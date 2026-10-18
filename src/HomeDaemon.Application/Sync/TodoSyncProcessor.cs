using System.Text.Json;
using HomeDaemon.Application.Abstractions;
using HomeDaemon.Application.Abstractions.Integrations;
using HomeDaemon.Domain.Chores;
using HomeDaemon.Domain.Sync;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HomeDaemon.Application.Sync;

/// <summary>
/// Dispatches pending outbox entries to Home Assistant after commit. It never runs inside
/// the transaction that produced the plan, so a slow or offline Home Assistant cannot block
/// the database. Each entry is persisted on its own so one failing operation never rolls
/// back the status of its siblings.
/// </summary>
public sealed class TodoSyncProcessor(
    IApplicationDbContext db,
    IHomeAssistantClient homeAssistant,
    TimeProvider timeProvider,
    IOptions<TodoSyncOptions> options,
    ILogger<TodoSyncProcessor> logger)
{
    public async Task<int> ProcessPendingAsync(CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var pending = await db.TodoSyncOutbox
            .Where(item => item.Status == TodoSyncStatus.Pending)
            .OrderBy(item => item.CreatedAtUtc)
            .ThenBy(item => item.OperationType)
            .Take(settings.BatchSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (pending.Count == 0)
        {
            return 0;
        }

        var processed = 0;
        foreach (var operation in pending)
        {
            try
            {
                await ApplyAsync(operation, cancellationToken).ConfigureAwait(false);
                operation.Status = TodoSyncStatus.Completed;
                operation.ProcessedAtUtc = timeProvider.GetUtcNow();
                operation.LastError = null;
                processed++;
            }
            catch (HomeAssistantRequestException ex) when (!ex.IsTransient)
            {
                MarkFailed(operation, ex.Message);
                TodoSyncLog.OperationRejected(logger, operation.Id, operation.OperationType, ex.Message);
            }
            catch (DbUpdateException ex)
            {
                MarkFailed(operation, ex.InnerException?.Message ?? ex.Message);
                TodoSyncLog.ConstraintViolation(logger, operation.Id, operation.OperationType, ex.InnerException?.Message ?? ex.Message);
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested
                && ex is HomeAssistantRequestException or HttpRequestException or TaskCanceledException)
            {
                operation.Attempts++;
                operation.LastError = ex.Message;
                if (operation.Attempts >= settings.MaxAttempts)
                {
                    operation.Status = TodoSyncStatus.Failed;
                }

                TodoSyncLog.TransientFailure(logger, operation.Id, operation.OperationType, operation.Attempts, ex.Message);
            }

            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return processed;
    }

    private static void MarkFailed(TodoSyncOutbox operation, string reason)
    {
        operation.Attempts++;
        operation.LastError = reason;
        operation.Status = TodoSyncStatus.Failed;
    }

    private async Task ApplyAsync(TodoSyncOutbox operation, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<TodoSyncPayload>(operation.Payload)
            ?? throw new HomeAssistantRequestException($"Outbox entry '{operation.Id}' has an invalid payload.", isTransient: false);

        var draft = new HomeAssistantTodoItemDraft(payload.Summary, payload.Description, payload.DueDate, HomeAssistantTodoStatus.NeedsAction);

        switch (operation.OperationType)
        {
            case TodoSyncOperationType.Create:
                await CreateAsync(operation, draft, cancellationToken).ConfigureAwait(false);
                break;

            case TodoSyncOperationType.Update:
                RequireItemUid(operation);
                await homeAssistant.UpdateTodoItemAsync(operation.TodoListEntityId, operation.ItemUid!, draft, cancellationToken).ConfigureAwait(false);
                await TouchLinkAsync(operation, cancellationToken).ConfigureAwait(false);
                break;

            case TodoSyncOperationType.Delete:
                if (operation.ItemUid is not null)
                {
                    await homeAssistant.DeleteTodoItemAsync(operation.TodoListEntityId, operation.ItemUid, cancellationToken).ConfigureAwait(false);
                }

                await RemoveLinksAsync(operation, cancellationToken).ConfigureAwait(false);
                break;

            default:
                throw new HomeAssistantRequestException($"Unsupported outbox operation '{operation.OperationType}'.", isTransient: false);
        }
    }

    private async Task CreateAsync(TodoSyncOutbox operation, HomeAssistantTodoItemDraft draft, CancellationToken cancellationToken)
    {
        if (operation.ChoreDefinitionId is not Guid choreId)
        {
            throw new HomeAssistantRequestException($"Outbox entry '{operation.Id}' cannot create an item without a chore.", isTransient: false);
        }

        var choreExists = await db.Chores.AnyAsync(chore => chore.Id == choreId, cancellationToken).ConfigureAwait(false);
        if (!choreExists)
        {
            throw new HomeAssistantRequestException($"Chore '{choreId}' no longer exists.", isTransient: false);
        }

        var alreadyLinked = await db.TodoItemLinks.AnyAsync(link => link.ChoreDefinitionId == choreId, cancellationToken).ConfigureAwait(false);
        if (alreadyLinked)
        {
            // A previous pass already created the item; treat the create as idempotent.
            return;
        }

        var created = await homeAssistant.CreateTodoItemAsync(operation.TodoListEntityId, draft, cancellationToken).ConfigureAwait(false);
        var now = timeProvider.GetUtcNow();
        db.TodoItemLinks.Add(new TodoItemLink
        {
            HouseholdId = operation.HouseholdId,
            ChoreDefinitionId = choreId,
            TodoListEntityId = operation.TodoListEntityId,
            ItemUid = created.Uid,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        });
    }

    private static void RequireItemUid(TodoSyncOutbox operation)
    {
        if (operation.ItemUid is null)
        {
            throw new HomeAssistantRequestException($"Outbox entry '{operation.Id}' cannot update an item without a UID.", isTransient: false);
        }
    }

    private async Task TouchLinkAsync(TodoSyncOutbox operation, CancellationToken cancellationToken)
    {
        if (operation.ChoreDefinitionId is not Guid choreId)
        {
            return;
        }

        var link = await db.TodoItemLinks
            .FirstOrDefaultAsync(item => item.ChoreDefinitionId == choreId, cancellationToken)
            .ConfigureAwait(false);

        if (link is null)
        {
            return;
        }

        link.UpdatedAtUtc = timeProvider.GetUtcNow();
    }

    private async Task RemoveLinksAsync(TodoSyncOutbox operation, CancellationToken cancellationToken)
    {
        if (operation.ChoreDefinitionId is not Guid choreId)
        {
            return;
        }

        var links = await db.TodoItemLinks
            .Where(item => item.ChoreDefinitionId == choreId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var link in links)
        {
            db.TodoItemLinks.Remove(link);
        }
    }
}

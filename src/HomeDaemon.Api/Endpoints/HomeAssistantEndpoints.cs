using HomeDaemon.Application.Abstractions;
using HomeDaemon.Application.Chores;
using HomeDaemon.Application.Planner;
using Microsoft.EntityFrameworkCore;

namespace HomeDaemon.Api.Endpoints;

public static class HomeAssistantEndpoints
{
    public static IEndpointRouteBuilder MapHomeAssistantEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/integrations/home-assistant").WithTags("Home Assistant");

        group.MapPost("/todo-completions", async (
            TodoCompletionRequest request,
            RecordHomeAssistantCompletionHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(
                new RecordHomeAssistantCompletionCommand(request.TodoListEntityId, request.ItemUid, request.CompletedByMemberId, request.IdempotencyKey),
                cancellationToken).ConfigureAwait(false);

            return Results.Ok(result);
        });

        group.MapPost("/todo-overrides", async (
            TodoOverrideRequest request,
            IApplicationDbContext db,
            UpdateOccurrenceHandler handler,
            CancellationToken cancellationToken) =>
        {
            var link = await db.TodoItemLinks
                .FirstOrDefaultAsync(item => item.TodoListEntityId == request.TodoListEntityId && item.ItemUid == request.ItemUid, cancellationToken)
                .ConfigureAwait(false);

            if (link is null)
            {
                return Results.NotFound();
            }

            var memberId = request.MemberId ?? await ResolvePlannedMemberAsync(db, link.ChoreDefinitionId, cancellationToken).ConfigureAwait(false);
            if (memberId is not Guid resolvedMember)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    ["memberId"] = ["A member must be provided when the item has no planned member."],
                });
            }

            var updated = await handler.HandleAsync(new UpdateOccurrenceCommand(link.ChoreDefinitionId, request.Date, resolvedMember), cancellationToken).ConfigureAwait(false);
            return Results.Ok(updated);
        });

        return group;
    }

    private static async Task<Guid?> ResolvePlannedMemberAsync(IApplicationDbContext db, Guid choreId, CancellationToken cancellationToken)
    {
        var plannedMemberId = await db.Chores
            .AsNoTracking()
            .Where(chore => chore.Id == choreId)
            .Select(chore => chore.PlannedMemberId)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return plannedMemberId;
    }
}

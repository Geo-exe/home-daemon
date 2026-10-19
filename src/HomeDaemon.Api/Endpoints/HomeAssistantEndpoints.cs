using HomeDaemon.Application.Abstractions;
using HomeDaemon.Application.Abstractions.Integrations;
using HomeDaemon.Application.Chores;
using HomeDaemon.Application.Households;
using HomeDaemon.Application.Planner;
using HomeDaemon.DataAccess.Integrations.HomeAssistant;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HomeDaemon.Api.Endpoints;

public static class HomeAssistantEndpoints
{
    public static IEndpointRouteBuilder MapHomeAssistantEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/integrations/home-assistant").WithTags("Home Assistant");

        group.MapGet("/entities", ListEntitiesAsync);

        // The integration's connection test: unlike the anonymous health probes this proves
        // that the machine API key is valid and names the household the instance serves.
        group.MapGet("/handshake", async (GetHouseholdHandler handler, CancellationToken cancellationToken) =>
        {
            var household = await handler.HandleAsync(new GetHouseholdQuery(), cancellationToken).ConfigureAwait(false);
            var version = typeof(HomeAssistantEndpoints).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
            return Results.Ok(new HomeAssistantHandshakeResponse(household.Id, household.Name, version));
        });

        group.MapPost("/todo-completions", async (
            TodoCompletionRequest request,
            RecordHomeAssistantCompletionHandler handler,
            CancellationToken cancellationToken) =>
        {
            var result = await handler.HandleAsync(
                new RecordHomeAssistantCompletionCommand(request.TodoListEntityId, request.ItemUid, request.CompletedByMemberId, request.IdempotencyKey),
                cancellationToken).ConfigureAwait(false);

            // No link means the item belongs to a list HomeDaemon does not manage: discarded
            // quietly so the integration never retries an event it can never record.
            return result is null ? Results.NoContent() : Results.Ok(result);
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

    private static async Task<IResult> ListEntitiesAsync(
        IHomeAssistantClient client,
        IOptions<HomeAssistantOptions> options,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        if (options.Value.BaseUrl is null)
        {
            return Results.Ok(new HomeAssistantEntitiesResponse(false, []));
        }

        try
        {
            var entities = await client.GetEntitiesAsync(cancellationToken).ConfigureAwait(false);
            return Results.Ok(new HomeAssistantEntitiesResponse(
                true,
                entities.Select(entity => new HomeAssistantEntityResponse(
                    entity.EntityId,
                    entity.Domain,
                    entity.FriendlyName)).ToList()));
        }
        catch (Exception exception) when (exception is HomeAssistantRequestException or HttpRequestException or TaskCanceledException)
        {
            // A broken integration must never block manual entry in the settings screens.
            ApiLog.HomeAssistantEntitiesFailed(
                loggerFactory.CreateLogger("HomeDaemon.Api.Endpoints.HomeAssistantEndpoints"),
                exception);

            return Results.Problem(
                statusCode: StatusCodes.Status502BadGateway,
                title: "Home Assistant unavailable",
                detail: "The Home Assistant entity list could not be loaded.");
        }
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

using HomeDaemon.Application.Households;
using HomeDaemon.Application.Scheduling;
using HomeDaemon.Domain.Scheduling;

namespace HomeDaemon.Api.Endpoints;

public static class SchedulingEndpoints
{
    public static IEndpointRouteBuilder MapSchedulingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/scheduling").WithTags("Scheduling");

        group.MapPost("/recalculations", async (
            RecalculateScheduleCommandHandler handler,
            CancellationToken cancellationToken) =>
        {
            var runId = await handler.HandleAsync(new RecalculateScheduleCommand(ScheduleRunTrigger.Manual), cancellationToken).ConfigureAwait(false);
            return Results.Accepted($"/api/v1/scheduling/recalculations/{runId}", new RecalculationAcceptedResponse(runId, "Running"));
        });

        group.MapGet("/recalculations", async (
            string? cursor,
            int? pageSize,
            GetHouseholdHandler getHousehold,
            GetScheduleRunsHandler handler,
            CancellationToken cancellationToken) =>
        {
            var household = await getHousehold.HandleAsync(new GetHouseholdQuery(), cancellationToken).ConfigureAwait(false);
            var page = await handler.HandleAsync(new GetScheduleRunsQuery(household.Id, cursor, pageSize), cancellationToken).ConfigureAwait(false);
            return Results.Ok(page);
        });

        group.MapGet("/recalculations/{runId:guid}", async (Guid runId, GetScheduleRunHandler handler, CancellationToken cancellationToken) =>
            Results.Ok(await handler.HandleAsync(new GetScheduleRunQuery(runId), cancellationToken).ConfigureAwait(false)));

        return group;
    }
}

using HomeDaemon.Application.Households;
using HomeDaemon.Application.Planner;

namespace HomeDaemon.Api.Endpoints;

public static class PlannerEndpoints
{
    public static IEndpointRouteBuilder MapPlannerEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/planner").WithTags("Planner");

        group.MapGet("/", async (
            DateOnly? from,
            DateOnly? to,
            GetHouseholdHandler getHousehold,
            GetPlannerHandler handler,
            TimeProvider timeProvider,
            CancellationToken cancellationToken) =>
        {
            var household = await getHousehold.HandleAsync(new GetHouseholdQuery(), cancellationToken).ConfigureAwait(false);
            var fromDate = from ?? Application.Common.HouseholdClock.Today(household.TimeZoneId, timeProvider);
            var toDate = to ?? fromDate.AddDays(household.PlanningHorizonDays);
            return Results.Ok(await handler.HandleAsync(new PlannerQuery(household.Id, fromDate, toDate), cancellationToken).ConfigureAwait(false));
        });

        group.MapPatch("/occurrences/{choreId:guid}", async (
            Guid choreId,
            UpdateOccurrenceRequest request,
            UpdateOccurrenceHandler handler,
            CancellationToken cancellationToken) =>
            Results.Ok(await handler.HandleAsync(new UpdateOccurrenceCommand(choreId, request.Date, request.MemberId), cancellationToken).ConfigureAwait(false)));

        group.MapPost("/occurrences/{choreId:guid}/unlock", async (
            Guid choreId,
            UnlockOccurrenceHandler handler,
            CancellationToken cancellationToken) =>
            Results.Ok(await handler.HandleAsync(new UnlockOccurrenceCommand(choreId), cancellationToken).ConfigureAwait(false)));

        return group;
    }
}

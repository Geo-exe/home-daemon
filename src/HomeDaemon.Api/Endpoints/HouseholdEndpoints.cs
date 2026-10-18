using HomeDaemon.Api.Auth;
using HomeDaemon.Application.Households;

namespace HomeDaemon.Api.Endpoints;

public static class HouseholdEndpoints
{
    public static IEndpointRouteBuilder MapHouseholdEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1").WithTags("Household");
        MapHousehold(group);
        MapMembers(group);
        MapRooms(group);
        return group;
    }

    private static void MapHousehold(RouteGroupBuilder group)
    {
        // Creating the household is a one-off administrative step: ordinary users reach the
        // app only once it exists, and the first-run setup runs as the administrator.
        group.MapPost("/households", async (CreateHouseholdRequest request, CreateHouseholdHandler handler, CancellationToken cancellationToken) =>
            Results.Created("/api/v1/household", await handler.HandleAsync(new CreateHouseholdCommand(request.Name, request.TimeZoneId, request.Currency), cancellationToken).ConfigureAwait(false)))
            .RequireAuthorization(AccountRoles.AdminPolicy);

        group.MapGet("/household", async (GetHouseholdHandler handler, CancellationToken cancellationToken) =>
            Results.Ok(await handler.HandleAsync(new GetHouseholdQuery(), cancellationToken).ConfigureAwait(false)));

        group.MapPut("/household", async (
            UpdateHouseholdRequest request,
            GetHouseholdHandler getHandler,
            UpdateHouseholdHandler handler,
            CancellationToken cancellationToken) =>
        {
            var household = await getHandler.HandleAsync(new GetHouseholdQuery(), cancellationToken).ConfigureAwait(false);
            var updated = await handler.HandleAsync(
                new UpdateHouseholdCommand(
                    household.Id,
                    request.Name,
                    request.TimeZoneId,
                    request.Currency,
                    request.PlanningHorizonDays,
                    request.DailyWindowStart,
                    request.DailyWindowEnd,
                    request.MaxChoreMinutesPerWeekday,
                    request.MaxChoreMinutesPerWeekendDay),
                cancellationToken).ConfigureAwait(false);

            return Results.Ok(updated);
        });
    }

    private static void MapMembers(RouteGroupBuilder group)
    {
        group.MapGet("/household/members", async (GetHouseholdHandler getHandler, GetMembersHandler handler, CancellationToken cancellationToken) =>
        {
            var household = await getHandler.HandleAsync(new GetHouseholdQuery(), cancellationToken).ConfigureAwait(false);
            return Results.Ok(await handler.HandleAsync(new GetMembersQuery(household.Id), cancellationToken).ConfigureAwait(false));
        });

        group.MapPost("/household/members", async (
            CreateMemberRequest request,
            GetHouseholdHandler getHandler,
            CreateMemberHandler handler,
            CancellationToken cancellationToken) =>
        {
            var household = await getHandler.HandleAsync(new GetHouseholdQuery(), cancellationToken).ConfigureAwait(false);
            var created = await handler.HandleAsync(
                new CreateMemberCommand(household.Id, request.Name, request.Color, request.IsAssignable, request.IsBudgetParticipant, request.TodoListEntityId, request.PersonEntityId, request.CalendarEntityIds),
                cancellationToken).ConfigureAwait(false);

            return Results.Created($"/api/v1/household/members/{created.Id}", created);
        });

        group.MapPut("/household/members/{memberId:guid}", async (
            Guid memberId,
            UpdateMemberRequest request,
            UpdateMemberHandler handler,
            CancellationToken cancellationToken) =>
            Results.Ok(await handler.HandleAsync(
                new UpdateMemberCommand(memberId, request.Name, request.Color, request.IsAssignable, request.IsBudgetParticipant, request.TodoListEntityId, request.PersonEntityId, request.CalendarEntityIds),
                cancellationToken).ConfigureAwait(false)));

        group.MapDelete("/household/members/{memberId:guid}", async (
            Guid memberId,
            DeleteMemberHandler handler,
            CancellationToken cancellationToken) =>
        {
            await handler.HandleAsync(new DeleteMemberCommand(memberId), cancellationToken).ConfigureAwait(false);
            return Results.NoContent();
        });
    }

    private static void MapRooms(RouteGroupBuilder group)
    {
        group.MapGet("/household/rooms", async (GetHouseholdHandler getHandler, GetRoomsHandler handler, CancellationToken cancellationToken) =>
        {
            var household = await getHandler.HandleAsync(new GetHouseholdQuery(), cancellationToken).ConfigureAwait(false);
            return Results.Ok(await handler.HandleAsync(new GetRoomsQuery(household.Id), cancellationToken).ConfigureAwait(false));
        });

        group.MapPost("/household/rooms", async (
            CreateRoomRequest request,
            GetHouseholdHandler getHandler,
            CreateRoomHandler handler,
            CancellationToken cancellationToken) =>
        {
            var household = await getHandler.HandleAsync(new GetHouseholdQuery(), cancellationToken).ConfigureAwait(false);
            var created = await handler.HandleAsync(new CreateRoomCommand(household.Id, request.Name, request.SortOrder), cancellationToken).ConfigureAwait(false);
            return Results.Created($"/api/v1/household/rooms/{created.Id}", created);
        });

        group.MapPut("/household/rooms/{roomId:guid}", async (
            Guid roomId,
            UpdateRoomRequest request,
            UpdateRoomHandler handler,
            CancellationToken cancellationToken) =>
            Results.Ok(await handler.HandleAsync(new UpdateRoomCommand(roomId, request.Name, request.SortOrder), cancellationToken).ConfigureAwait(false)));

        group.MapDelete("/household/rooms/{roomId:guid}", async (
            Guid roomId,
            DeleteRoomHandler handler,
            CancellationToken cancellationToken) =>
        {
            await handler.HandleAsync(new DeleteRoomCommand(roomId), cancellationToken).ConfigureAwait(false);
            return Results.NoContent();
        });
    }
}

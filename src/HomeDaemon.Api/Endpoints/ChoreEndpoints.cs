using System.Security.Claims;
using HomeDaemon.Application.Chores;
using HomeDaemon.Application.Common;
using HomeDaemon.Application.Households;
using HomeDaemon.DataAccess.Persistence;
using HomeDaemon.Domain.Chores;
using Microsoft.AspNetCore.Identity;

namespace HomeDaemon.Api.Endpoints;

public static class ChoreEndpoints
{
    public static IEndpointRouteBuilder MapChoreEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/chores").WithTags("Chores");
        MapListAndCreate(group);
        MapItemOperations(group);
        MapHistory(group);
        return group;
    }

    private static void MapListAndCreate(RouteGroupBuilder group)
    {
        group.MapGet("/", async (
            Guid? roomId,
            bool? isActive,
            GetHouseholdHandler getHousehold,
            GetChoresHandler handler,
            CancellationToken cancellationToken) =>
        {
            var household = await getHousehold.HandleAsync(new GetHouseholdQuery(), cancellationToken).ConfigureAwait(false);
            return Results.Ok(await handler.HandleAsync(new GetChoresQuery(household.Id, roomId, isActive), cancellationToken).ConfigureAwait(false));
        });

        group.MapPost("/", async (
            CreateChoreRequest request,
            GetHouseholdHandler getHousehold,
            CreateChoreHandler handler,
            CancellationToken cancellationToken) =>
        {
            var household = await getHousehold.HandleAsync(new GetHouseholdQuery(), cancellationToken).ConfigureAwait(false);
            var created = await handler.HandleAsync(
                new CreateChoreCommand(
                    household.Id,
                    request.Title,
                    request.Description,
                    request.RoomId,
                    request.IsActive,
                    request.EstimatedMinutes,
                    request.EffortWeight,
                    request.PreferSameRoomGrouping,
                    request.Frequency,
                    request.Interval,
                    request.Mode,
                    request.AnchorDate,
                    request.AssignmentPolicy,
                    request.FixedAssigneeMemberId),
                cancellationToken).ConfigureAwait(false);

            return Results.Created($"/api/v1/chores/{created.Id}", created);
        });
    }

    private static void MapItemOperations(RouteGroupBuilder group)
    {
        group.MapGet("/{choreId:guid}", async (Guid choreId, GetChoreHandler handler, CancellationToken cancellationToken) =>
            Results.Ok(await handler.HandleAsync(new GetChoreQuery(choreId), cancellationToken).ConfigureAwait(false)));

        group.MapPut("/{choreId:guid}", async (Guid choreId, UpdateChoreRequest request, UpdateChoreHandler handler, CancellationToken cancellationToken) =>
            Results.Ok(await handler.HandleAsync(
                new UpdateChoreCommand(
                    choreId,
                    request.Title,
                    request.Description,
                    request.RoomId,
                    request.IsActive,
                    request.EstimatedMinutes,
                    request.EffortWeight,
                    request.PreferSameRoomGrouping,
                    request.Frequency,
                    request.Interval,
                    request.Mode,
                    request.AnchorDate,
                    request.AssignmentPolicy,
                    request.FixedAssigneeMemberId),
                cancellationToken).ConfigureAwait(false)));

        group.MapDelete("/{choreId:guid}", async (Guid choreId, DeleteChoreHandler handler, CancellationToken cancellationToken) =>
        {
            await handler.HandleAsync(new DeleteChoreCommand(choreId), cancellationToken).ConfigureAwait(false);
            return Results.NoContent();
        });

        group.MapPost("/{choreId:guid}/restore", async (Guid choreId, RestoreChoreHandler handler, CancellationToken cancellationToken) =>
            Results.Ok(await handler.HandleAsync(new RestoreChoreCommand(choreId), cancellationToken).ConfigureAwait(false)));

        group.MapPost("/{choreId:guid}/complete", async (
            Guid choreId,
            CompleteChoreRequest request,
            ClaimsPrincipal principal,
            UserManager<ApplicationUser> users,
            CompleteChoreHandler handler,
            CancellationToken cancellationToken) =>
        {
            var account = await users.GetUserAsync(principal).ConfigureAwait(false)
                ?? throw new EntityNotFoundException("The current account no longer exists.");

            return Results.Ok(await handler.HandleAsync(
                new CompleteChoreCommand(
                    choreId,
                    request.CompletedDate,
                    account.HouseholdMemberId,
                    CompletionSource.Manual,
                    request.IdempotencyKey,
                    account.Id),
                cancellationToken).ConfigureAwait(false));
        });
    }

    private static void MapHistory(RouteGroupBuilder group)
    {
        group.MapGet("/history", async (
            Guid? choreId,
            Guid? memberId,
            DateOnly? from,
            DateOnly? to,
            CompletionSource? source,
            string? cursor,
            int? pageSize,
            GetHouseholdHandler getHousehold,
            GetChoreHistoryHandler handler,
            CancellationToken cancellationToken) =>
        {
            var household = await getHousehold.HandleAsync(new GetHouseholdQuery(), cancellationToken).ConfigureAwait(false);
            var page = await handler.HandleAsync(
                new GetChoreHistoryQuery(household.Id, choreId, memberId, from, to, source, cursor, pageSize),
                cancellationToken).ConfigureAwait(false);

            return Results.Ok(page);
        });
    }
}

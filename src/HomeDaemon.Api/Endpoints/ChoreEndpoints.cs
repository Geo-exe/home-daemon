using System.Security.Claims;
using HomeDaemon.Api.Auth;
using HomeDaemon.Application.Chores;
using HomeDaemon.Application.Common;
using HomeDaemon.Application.Households;
using HomeDaemon.Application.Scheduling;
using HomeDaemon.DataAccess.Persistence;
using HomeDaemon.Domain.Chores;
using HomeDaemon.Domain.Scheduling;
using Microsoft.AspNetCore.Identity;

namespace HomeDaemon.Api.Endpoints;

public static class ChoreEndpoints
{
    public static IEndpointRouteBuilder MapChoreEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/chores").WithTags("Chores");
        MapListAndCreate(group);
        MapImports(group);
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

    private static void MapImports(RouteGroupBuilder group)
    {
        // Importing rewrites the household's whole chore catalog, so it is administrator-only
        // even though individual chore writes are available to every signed-in user.
        group.MapPost("/imports", ImportChoresAsync)
            .RequireAuthorization(AccountRoles.AdminPolicy);
    }

    private static async Task<IResult> ImportChoresAsync(
        ImportChoresRequest request,
        GetHouseholdHandler getHousehold,
        ImportChoresHandler handler,
        RecalculateScheduleCommandHandler recalculate,
        CancellationToken cancellationToken)
    {
        var household = await getHousehold.HandleAsync(new GetHouseholdQuery(), cancellationToken).ConfigureAwait(false);
        var result = await handler.HandleAsync(
            new ImportChoresCommand(
                household.Id,
                (request.Chores ?? []).Select(row => new ImportChoreRow(
                    row.Line,
                    row.SourceId,
                    row.Title,
                    row.Description,
                    row.Room,
                    row.EstimatedMinutes,
                    row.IsActive,
                    row.EffortWeight,
                    row.PreferSameRoomGrouping,
                    row.Frequency,
                    row.Interval,
                    row.Mode,
                    row.AnchorDate,
                    row.AssignmentPolicy,
                    row.FixedAssigneeMemberId,
                    row.SyntheticCompletionDate,
                    row.Completions ?? [])).ToList(),
                (request.Rooms ?? []).Select(mapping => new ImportChoreRoomMapping(
                    mapping.SourceName,
                    mapping.RoomId,
                    mapping.Create)).ToList()),
            cancellationToken).ConfigureAwait(false);

        // One recalculation for the whole file, not one per chore: imported chores are
        // deliberately left unplanned until the scheduler places them.
        Guid? runId = result.Imported > 0
            ? await recalculate.HandleAsync(new RecalculateScheduleCommand(ScheduleRunTrigger.Manual), cancellationToken).ConfigureAwait(false)
            : null;

        return Results.Ok(new ImportChoresResponse(
            result.Imported,
            result.Skipped,
            result.CompletionsImported,
            result.CompletionsSkipped,
            runId));
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

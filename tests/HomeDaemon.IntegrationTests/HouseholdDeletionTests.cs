using HomeDaemon.Application.Chores;
using HomeDaemon.Application.Common;
using HomeDaemon.Application.Households;
using HomeDaemon.Application.Recurrence;
using HomeDaemon.Domain.Chores;
using HomeDaemon.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;

namespace HomeDaemon.IntegrationTests;

public sealed class HouseholdDeletionTests
{
    [Fact]
    public async Task UpdatingARoom_ChangesItsNameAndOrder()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var db = database.Context;
        var time = TestTimeProvider.At(TestSeed.DefaultDate);
        var household = await TestSeed.HouseholdAsync(db, time);
        var room = await TestSeed.RoomAsync(db, time, household.Id, "Kitchen");

        var updated = await new UpdateRoomHandler(db, time)
            .HandleAsync(new UpdateRoomCommand(room.Id, "Cucina", 3), TestContext.Current.CancellationToken);

        updated.Name.Should().Be("Cucina");
        updated.SortOrder.Should().Be(3);
    }

    [Fact]
    public async Task DeletingARoomThatStillHasChores_IsAConflict()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var db = database.Context;
        var time = TestTimeProvider.At(TestSeed.DefaultDate);
        var household = await TestSeed.HouseholdAsync(db, time);
        var room = await TestSeed.RoomAsync(db, time, household.Id);
        await TestSeed.ChoreAsync(db, time, household.Id, roomId: room.Id);

        var delete = () => new DeleteRoomHandler(db).HandleAsync(new DeleteRoomCommand(room.Id), TestContext.Current.CancellationToken);

        await delete.Should().ThrowAsync<EntityConflictException>();
    }

    [Fact]
    public async Task DeletingARoomReferencedOnlyByADeletedChore_IsStillAConflict()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var db = database.Context;
        var time = TestTimeProvider.At(TestSeed.DefaultDate);
        var household = await TestSeed.HouseholdAsync(db, time);
        var room = await TestSeed.RoomAsync(db, time, household.Id);
        var chore = await TestSeed.ChoreAsync(db, time, household.Id, roomId: room.Id);

        await new DeleteChoreHandler(db, time).HandleAsync(new DeleteChoreCommand(chore.Id), TestContext.Current.CancellationToken);

        var delete = () => new DeleteRoomHandler(db).HandleAsync(new DeleteRoomCommand(room.Id), TestContext.Current.CancellationToken);

        await delete.Should().ThrowAsync<EntityConflictException>();
    }

    [Fact]
    public async Task DeletingAnUnreferencedRoom_RemovesIt()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var db = database.Context;
        var time = TestTimeProvider.At(TestSeed.DefaultDate);
        var household = await TestSeed.HouseholdAsync(db, time);
        var room = await TestSeed.RoomAsync(db, time, household.Id);

        await new DeleteRoomHandler(db).HandleAsync(new DeleteRoomCommand(room.Id), TestContext.Current.CancellationToken);

        (await db.Rooms.CountAsync(TestContext.Current.CancellationToken)).Should().Be(0);
    }

    [Fact]
    public async Task DeletingAMemberWithCompletionHistory_IsAConflict()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var db = database.Context;
        var time = TestTimeProvider.At(TestSeed.DefaultDate);
        var household = await TestSeed.HouseholdAsync(db, time);
        var member = await TestSeed.MemberAsync(db, time, household.Id);
        var chore = await TestSeed.ChoreAsync(db, time, household.Id);
        await TestSeed.AddCompletionAsync(db, household.Id, chore.Id, TestSeed.DefaultDate, time.GetUtcNow(), member.Id);

        var delete = () => new DeleteMemberHandler(db).HandleAsync(new DeleteMemberCommand(member.Id), TestContext.Current.CancellationToken);

        await delete.Should().ThrowAsync<EntityConflictException>();
    }

    [Fact]
    public async Task DeletingAMemberWhoIsAFixedAssignee_IsAConflict()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var db = database.Context;
        var time = TestTimeProvider.At(TestSeed.DefaultDate);
        var household = await TestSeed.HouseholdAsync(db, time);
        var member = await TestSeed.MemberAsync(db, time, household.Id);

        await new CreateChoreHandler(db, new ChoreRecurrenceService(new RecurrenceCalculator()), time).HandleAsync(
            new CreateChoreCommand(
                household.Id,
                "Clean oven",
                null,
                null,
                true,
                30,
                null,
                false,
                RecurrenceFrequency.Daily,
                1,
                RecurrenceMode.Dynamic,
                null,
                AssignmentPolicy.SpecificMember,
                member.Id),
            TestContext.Current.CancellationToken);

        var delete = () => new DeleteMemberHandler(db).HandleAsync(new DeleteMemberCommand(member.Id), TestContext.Current.CancellationToken);

        await delete.Should().ThrowAsync<EntityConflictException>();
    }

    [Fact]
    public async Task DeletingAMemberReferencedByTheBudget_IsAConflict()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var db = database.Context;
        var time = TestTimeProvider.At(TestSeed.DefaultDate);
        var household = await TestSeed.HouseholdAsync(db, time);
        var member = await TestSeed.MemberAsync(db, time, household.Id, isBudgetParticipant: true);
        await TestSeed.BudgetIncomeAsync(db, time, household.Id, member.Id, 2025, 1, 1000);

        var delete = () => new DeleteMemberHandler(db).HandleAsync(new DeleteMemberCommand(member.Id), TestContext.Current.CancellationToken);

        await delete.Should().ThrowAsync<EntityConflictException>();
    }

    [Fact]
    public async Task DeletingAnUnreferencedMember_RemovesThemAndTheirCalendars()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var db = database.Context;
        var time = TestTimeProvider.At(TestSeed.DefaultDate);
        var household = await TestSeed.HouseholdAsync(db, time);
        var member = await TestSeed.MemberAsync(db, time, household.Id);

        await new DeleteMemberHandler(db).HandleAsync(new DeleteMemberCommand(member.Id), TestContext.Current.CancellationToken);

        (await db.HouseholdMembers.CountAsync(TestContext.Current.CancellationToken)).Should().Be(0);
        (await db.HouseholdMemberCalendars.CountAsync(TestContext.Current.CancellationToken)).Should().Be(0);
    }
}

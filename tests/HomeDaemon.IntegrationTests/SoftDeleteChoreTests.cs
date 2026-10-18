using HomeDaemon.Application.Abstractions;
using HomeDaemon.Application.Chores;
using HomeDaemon.Application.Common;
using HomeDaemon.Application.Households;
using HomeDaemon.Application.Planner;
using HomeDaemon.Application.Scheduling;
using HomeDaemon.Application.Sync;
using HomeDaemon.DataAccess.Persistence;
using HomeDaemon.Domain.Chores;
using HomeDaemon.Domain.Scheduling;
using HomeDaemon.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace HomeDaemon.IntegrationTests;

public sealed class SoftDeleteChoreTests
{
    [Fact]
    public async Task DeletingAChore_HidesItFromCatalogAndDetailButKeepsHistory()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var db = database.Context;
        var time = TestTimeProvider.At(TestSeed.DefaultDate);
        var household = await TestSeed.HouseholdAsync(db, time);
        var member = await TestSeed.MemberAsync(db, time, household.Id);
        var chore = await TestSeed.ChoreAsync(db, time, household.Id);
        await TestSeed.AddCompletionAsync(db, household.Id, chore.Id, TestSeed.DefaultDate, time.GetUtcNow(), member.Id);

        await new DeleteChoreHandler(db, time).HandleAsync(new DeleteChoreCommand(chore.Id), TestContext.Current.CancellationToken);

        var list = await new GetChoresHandler(db)
            .HandleAsync(new GetChoresQuery(household.Id, null, null), TestContext.Current.CancellationToken);
        list.Should().BeEmpty();

        var getDetail = () => new GetChoreHandler(db).HandleAsync(new GetChoreQuery(chore.Id), TestContext.Current.CancellationToken);
        await getDetail.Should().ThrowAsync<EntityNotFoundException>();

        // The row is still there, only hidden; that is what keeps the history foreign key valid.
        (await db.Chores.IgnoreQueryFilters().CountAsync(TestContext.Current.CancellationToken)).Should().Be(1);
        (await db.CompletionHistory.CountAsync(TestContext.Current.CancellationToken)).Should().Be(1);

        var history = await new GetChoreHistoryHandler(db).HandleAsync(
            new GetChoreHistoryQuery(household.Id, null, null, null, null, null, null, null),
            TestContext.Current.CancellationToken);

        history.Items.Should().ContainSingle();
        history.Items[0].ChoreTitle.Should().Be("Clean oven");
    }

    [Fact]
    public async Task DeletingAChore_RemovesItFromThePlanner()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var db = database.Context;
        var time = TestTimeProvider.At(TestSeed.DefaultDate);
        var household = await TestSeed.HouseholdAsync(db, time);
        await TestSeed.MemberAsync(db, time, household.Id);
        var chore = await TestSeed.ChoreAsync(db, time, household.Id);

        await RunRecalculationAsync(db, time, household.Id);

        var before = await new GetPlannerHandler(db, new AvailabilityMatrixBuilder(new NoOpHomeAssistantClient()))
            .HandleAsync(new PlannerQuery(household.Id, TestSeed.DefaultDate, TestSeed.DefaultDate.AddDays(7)), TestContext.Current.CancellationToken);
        before.Days.SelectMany(day => day.Occurrences).Should().ContainSingle(item => item.ChoreId == chore.Id);

        await new DeleteChoreHandler(db, time).HandleAsync(new DeleteChoreCommand(chore.Id), TestContext.Current.CancellationToken);

        var after = await new GetPlannerHandler(db, new AvailabilityMatrixBuilder(new NoOpHomeAssistantClient()))
            .HandleAsync(new PlannerQuery(household.Id, TestSeed.DefaultDate, TestSeed.DefaultDate.AddDays(7)), TestContext.Current.CancellationToken);
        after.Days.SelectMany(day => day.Occurrences).Should().NotContain(item => item.ChoreId == chore.Id);
        after.Unscheduled.Should().NotContain(item => item.ChoreId == chore.Id);
    }

    [Fact]
    public async Task RestoringADeletedChore_BringsItBackUnplanned()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var db = database.Context;
        var time = TestTimeProvider.At(TestSeed.DefaultDate);
        var household = await TestSeed.HouseholdAsync(db, time);
        var chore = await TestSeed.ChoreAsync(db, time, household.Id);

        await new DeleteChoreHandler(db, time).HandleAsync(new DeleteChoreCommand(chore.Id), TestContext.Current.CancellationToken);

        var restored = await new RestoreChoreHandler(db, time)
            .HandleAsync(new RestoreChoreCommand(chore.Id), TestContext.Current.CancellationToken);

        restored.Id.Should().Be(chore.Id);
        restored.ScheduleState.Should().Be(ChoreScheduleState.Unplanned);
        (await db.Chores.CountAsync(TestContext.Current.CancellationToken)).Should().Be(1);
    }

    [Fact]
    public async Task RestoringAChoreThatWasNotDeleted_IsAConflict()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var db = database.Context;
        var time = TestTimeProvider.At(TestSeed.DefaultDate);
        var household = await TestSeed.HouseholdAsync(db, time);
        var chore = await TestSeed.ChoreAsync(db, time, household.Id);

        var restore = () => new RestoreChoreHandler(db, time)
            .HandleAsync(new RestoreChoreCommand(chore.Id), TestContext.Current.CancellationToken);

        await restore.Should().ThrowAsync<EntityConflictException>();
    }

    private static async Task RunRecalculationAsync(HomeDaemonDbContext db, TimeProvider time, Guid householdId)
    {
        var run = new ScheduleRun
        {
            HouseholdId = householdId,
            Trigger = ScheduleRunTrigger.Manual,
            Status = ScheduleRunStatus.Running,
            StartedAtUtc = time.GetUtcNow(),
        };

        db.ScheduleRuns.Add(run);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var executor = new RecalculationExecutor(
            db,
            new AvailabilityMatrixBuilder(new NoOpHomeAssistantClient()),
            new ChoreSyncService(db, time),
            new RecalculationGate(),
            Options.Create(new SchedulingOptions()),
            time,
            NullLogger<RecalculationExecutor>.Instance);

        await executor.ExecuteAsync(run.Id, TestContext.Current.CancellationToken);
    }
}

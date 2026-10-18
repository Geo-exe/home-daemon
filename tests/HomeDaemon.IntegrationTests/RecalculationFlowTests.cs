using HomeDaemon.Application.Abstractions;
using HomeDaemon.Application.Chores;
using HomeDaemon.Application.Households;
using HomeDaemon.Application.Recurrence;
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

public sealed class RecalculationFlowTests
{
    [Fact]
    public async Task Recalculation_PlacesAChoreOnItsTheoreticalDueDateAndRecordsTheRun()
    {
        var today = new DateOnly(2026, 6, 10);
        await using var database = await SqliteTestDatabase.CreateAsync();
        var context = database.Context;
        var timeProvider = TestTimeProvider.At(today);
        var recurrence = new ChoreRecurrenceService(new RecurrenceCalculator());

        var household = await new CreateHouseholdHandler(context, timeProvider)
            .HandleAsync(new CreateHouseholdCommand("Home", "UTC"), CancellationToken.None);

        await new CreateMemberHandler(context, timeProvider)
            .HandleAsync(new CreateMemberCommand(household.Id, "Alice", null, true, "todo.alice", null, []), CancellationToken.None);

        var chore = await new CreateChoreHandler(context, recurrence, timeProvider)
            .HandleAsync(
                new CreateChoreCommand(household.Id, "Clean oven", null, null, true, 30, null, false, RecurrenceFrequency.Daily, 1, RecurrenceMode.Dynamic, null, AssignmentPolicy.Both, null),
                CancellationToken.None);

        var run = new ScheduleRun
        {
            HouseholdId = household.Id,
            Trigger = ScheduleRunTrigger.Manual,
            Status = ScheduleRunStatus.Running,
            StartedAtUtc = timeProvider.GetUtcNow(),
        };

        context.ScheduleRuns.Add(run);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var executor = new RecalculationExecutor(
            context,
            new AvailabilityMatrixBuilder(new NoOpHomeAssistantClient()),
            new ChoreSyncService(context, timeProvider),
            new RecalculationGate(),
            Options.Create(new SchedulingOptions()),
            timeProvider,
            NullLogger<RecalculationExecutor>.Instance);

        await executor.ExecuteAsync(run.Id, CancellationToken.None);

        var storedRun = await context.ScheduleRuns.AsNoTracking().FirstAsync(item => item.Id == run.Id, TestContext.Current.CancellationToken);
        storedRun.Status.Should().Be(ScheduleRunStatus.Succeeded);
        storedRun.ScheduledCount.Should().Be(1);

        var storedChore = await context.Chores.AsNoTracking().FirstAsync(item => item.Id == chore.Id, TestContext.Current.CancellationToken);
        storedChore.ScheduleState.Should().Be(ChoreScheduleState.Scheduled);
        storedChore.NextDueDate.Should().Be(today);
    }
}

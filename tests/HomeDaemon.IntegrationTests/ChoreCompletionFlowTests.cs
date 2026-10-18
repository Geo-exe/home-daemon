using HomeDaemon.Application.Abstractions;
using HomeDaemon.Application.Chores;
using HomeDaemon.Application.Households;
using HomeDaemon.Application.Recurrence;
using HomeDaemon.Application.Sync;
using HomeDaemon.DataAccess.Persistence;
using HomeDaemon.Domain.Chores;
using HomeDaemon.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;

namespace HomeDaemon.IntegrationTests;

public sealed class ChoreCompletionFlowTests
{
    private static readonly DateOnly Today = new(2026, 6, 10);

    [Fact]
    public async Task CompletingAChore_RecordsHistoryAdvancesTheCycleAndQueuesSync()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var context = database.Context;
        var timeProvider = TestTimeProvider.At(Today);
        var recurrence = new ChoreRecurrenceService(new RecurrenceCalculator());

        var household = await SeedHouseholdAsync(context, timeProvider);
        var member = await SeedMemberAsync(context, timeProvider, household.Id);
        var chore = await CreateDailyChoreAsync(context, timeProvider, recurrence, household.Id);

        var completion = new ChoreCompletionService(context, recurrence, new ChoreSyncService(context, timeProvider), new RecalculationGate(), timeProvider);
        var handler = new CompleteChoreHandler(completion);

        var result = await handler.HandleAsync(new CompleteChoreCommand(chore.Id, null, member.Id, CompletionSource.Manual, "key-1"), CancellationToken.None);

        result.IsDuplicate.Should().BeFalse();

        var stored = await context.Chores.AsNoTracking().FirstAsync(item => item.Id == chore.Id, TestContext.Current.CancellationToken);
        stored.LastCompletedDate.Should().Be(Today);
        stored.NextOccurrenceDate.Should().Be(Today.AddDays(1));
        stored.NextDueDate.Should().Be(Today.AddDays(1));
        stored.ScheduleState.Should().Be(ChoreScheduleState.Scheduled);

        (await context.CompletionHistory.CountAsync(TestContext.Current.CancellationToken)).Should().Be(1);
        (await context.TodoSyncOutbox.CountAsync(TestContext.Current.CancellationToken)).Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ARepeatedNotificationWithTheSameKey_DoesNotCreateASecondCompletion()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var context = database.Context;
        var timeProvider = TestTimeProvider.At(Today);
        var recurrence = new ChoreRecurrenceService(new RecurrenceCalculator());

        var household = await SeedHouseholdAsync(context, timeProvider);
        var member = await SeedMemberAsync(context, timeProvider, household.Id);
        var chore = await CreateDailyChoreAsync(context, timeProvider, recurrence, household.Id);

        var completion = new ChoreCompletionService(context, recurrence, new ChoreSyncService(context, timeProvider), new RecalculationGate(), timeProvider);
        var handler = new CompleteChoreHandler(completion);
        var command = new CompleteChoreCommand(chore.Id, null, member.Id, CompletionSource.Manual, "duplicate");

        await handler.HandleAsync(command, CancellationToken.None);
        var second = await handler.HandleAsync(command, CancellationToken.None);

        second.IsDuplicate.Should().BeTrue();
        (await context.CompletionHistory.CountAsync(TestContext.Current.CancellationToken)).Should().Be(1);
    }

    private static async Task<HouseholdDto> SeedHouseholdAsync(HomeDaemonDbContext context, TimeProvider timeProvider)
    {
        var handler = new CreateHouseholdHandler(context, timeProvider);
        return await handler.HandleAsync(new CreateHouseholdCommand("Home", "UTC"), CancellationToken.None);
    }

    private static async Task<HouseholdMemberDto> SeedMemberAsync(HomeDaemonDbContext context, TimeProvider timeProvider, Guid householdId)
    {
        var handler = new CreateMemberHandler(context, timeProvider);
        return await handler.HandleAsync(
            new CreateMemberCommand(householdId, "Alice", null, true, false, "todo.alice", null, []),
            CancellationToken.None);
    }

    private static async Task<ChoreDetailDto> CreateDailyChoreAsync(
        HomeDaemonDbContext context,
        TimeProvider timeProvider,
        ChoreRecurrenceService recurrence,
        Guid householdId)
    {
        var handler = new CreateChoreHandler(context, recurrence, timeProvider);
        return await handler.HandleAsync(
            new CreateChoreCommand(
                householdId,
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
                AssignmentPolicy.Both,
                null),
            CancellationToken.None);
    }
}

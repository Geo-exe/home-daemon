using HomeDaemon.Application.Scheduling;
using HomeDaemon.Domain.Scheduling;
using HomeDaemon.IntegrationTests.Support;

namespace HomeDaemon.IntegrationTests;

public sealed class ScheduleRunsListTests
{
    [Fact]
    public async Task ScheduleRuns_AreReturnedNewestFirstAcrossPages()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var db = database.Context;
        var time = TestTimeProvider.At(TestSeed.DefaultDate);
        var household = await TestSeed.HouseholdAsync(db, time);

        for (var index = 0; index < 5; index++)
        {
            db.ScheduleRuns.Add(new ScheduleRun
            {
                HouseholdId = household.Id,
                Trigger = ScheduleRunTrigger.Manual,
                Status = ScheduleRunStatus.Succeeded,
                StartedAtUtc = time.GetUtcNow().AddHours(index),
                CompletedAtUtc = time.GetUtcNow().AddHours(index).AddMinutes(1),
            });
        }

        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var handler = new GetScheduleRunsHandler(db);
        var collected = new List<ScheduleRunDto>();
        string? cursor = null;

        do
        {
            var page = await handler.HandleAsync(
                new GetScheduleRunsQuery(household.Id, cursor, 2),
                TestContext.Current.CancellationToken);

            collected.AddRange(page.Items);
            cursor = page.NextCursor;
        }
        while (cursor is not null);

        collected.Should().HaveCount(5);
        collected.Select(run => run.StartedAtUtc).Should().BeInDescendingOrder();
        collected.Select(run => run.Id).Should().OnlyHaveUniqueItems();
    }
}

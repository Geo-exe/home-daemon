using HomeDaemon.Application.Chores;
using HomeDaemon.Application.Common;
using HomeDaemon.Domain.Chores;
using HomeDaemon.IntegrationTests.Support;

namespace HomeDaemon.IntegrationTests;

public sealed class ChoreHistoryPaginationTests
{
    [Fact]
    public async Task History_PagesThroughEveryCompletionWithoutGapsOrDuplicates()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var db = database.Context;
        var time = TestTimeProvider.At(TestSeed.DefaultDate);
        var household = await TestSeed.HouseholdAsync(db, time);
        var member = await TestSeed.MemberAsync(db, time, household.Id);
        var chore = await TestSeed.ChoreAsync(db, time, household.Id);

        for (var day = 0; day < 5; day++)
        {
            await TestSeed.AddCompletionAsync(
                db,
                household.Id,
                chore.Id,
                TestSeed.DefaultDate.AddDays(-day),
                time.GetUtcNow().AddMinutes(day),
                member.Id);
        }

        var handler = new GetChoreHistoryHandler(db);
        var collected = new List<ChoreHistoryItemDto>();
        string? cursor = null;

        do
        {
            var page = await handler.HandleAsync(
                new GetChoreHistoryQuery(household.Id, null, null, null, null, null, cursor, 2),
                TestContext.Current.CancellationToken);

            collected.AddRange(page.Items);
            cursor = page.NextCursor;
        }
        while (cursor is not null);

        collected.Should().HaveCount(5);
        collected.Select(item => item.Id).Should().OnlyHaveUniqueItems();
        collected.Select(item => item.CompletedDate).Should().BeInDescendingOrder();
    }

    [Fact]
    public async Task History_AppliesTheMemberAndSourceFilters()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var db = database.Context;
        var time = TestTimeProvider.At(TestSeed.DefaultDate);
        var household = await TestSeed.HouseholdAsync(db, time);
        var alice = await TestSeed.MemberAsync(db, time, household.Id, "Alice");
        var bob = await TestSeed.MemberAsync(db, time, household.Id, "Bob");
        var chore = await TestSeed.ChoreAsync(db, time, household.Id);

        await TestSeed.AddCompletionAsync(db, household.Id, chore.Id, TestSeed.DefaultDate, time.GetUtcNow(), alice.Id, CompletionSource.Manual);
        await TestSeed.AddCompletionAsync(db, household.Id, chore.Id, TestSeed.DefaultDate.AddDays(-1), time.GetUtcNow().AddMinutes(1), bob.Id, CompletionSource.HomeAssistant);

        var handler = new GetChoreHistoryHandler(db);

        var alicePage = await handler.HandleAsync(
            new GetChoreHistoryQuery(household.Id, null, alice.Id, null, null, null, null, null),
            TestContext.Current.CancellationToken);
        alicePage.Items.Should().ContainSingle();

        var homeAssistantPage = await handler.HandleAsync(
            new GetChoreHistoryQuery(household.Id, null, null, null, null, CompletionSource.HomeAssistant, null, null),
            TestContext.Current.CancellationToken);
        homeAssistantPage.Items.Should().ContainSingle();
        homeAssistantPage.Items[0].CompletedByMemberName.Should().Be("Bob");
    }

    [Fact]
    public async Task History_RejectsAMalformedCursor()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var db = database.Context;
        var time = TestTimeProvider.At(TestSeed.DefaultDate);
        var household = await TestSeed.HouseholdAsync(db, time);

        var handler = new GetChoreHistoryHandler(db);
        var request = () => handler.HandleAsync(
            new GetChoreHistoryQuery(household.Id, null, null, null, null, null, "not-a-cursor", null),
            TestContext.Current.CancellationToken);

        await request.Should().ThrowAsync<ApplicationValidationException>();
    }
}

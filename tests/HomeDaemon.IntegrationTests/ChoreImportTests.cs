using HomeDaemon.Application.Chores;
using HomeDaemon.Application.Common;
using HomeDaemon.Application.Recurrence;
using HomeDaemon.Domain.Chores;
using HomeDaemon.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;

namespace HomeDaemon.IntegrationTests;

/// <summary>
/// The chore import is atomic and idempotent: it creates definitions, imports the historical
/// executions without attribution, and synthesises one completion for a chore that was created
/// in the source but never tracked there.
/// </summary>
public sealed class ChoreImportTests
{
    [Fact]
    public async Task ImportCreatesChoresHistoryAndSyntheticCompletion()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var db = database.Context;
        var time = TestTimeProvider.At(new DateOnly(2026, 10, 20));
        var household = await TestSeed.HouseholdAsync(db, time);
        var member = await TestSeed.MemberAsync(db, time, household.Id, "Francesco");

        var handler = new ImportChoresHandler(db, new ChoreRecurrenceService(new RecurrenceCalculator()), time);
        var command = new ImportChoresCommand(
            household.Id,
            [
                new ImportChoreRow(
                    2,
                    "4",
                    "Pulire Vetri",
                    null,
                    "Cucina",
                    90,
                    true,
                    null,
                    false,
                    RecurrenceFrequency.FixedIntervalDays,
                    60,
                    RecurrenceMode.Dynamic,
                    null,
                    AssignmentPolicy.SpecificMember,
                    member.Id,
                    null,
                    [new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 10)]),
                new ImportChoreRow(
                    3,
                    "5",
                    "Pulire Termosifoni",
                    null,
                    "Cucina",
                    30,
                    true,
                    null,
                    false,
                    RecurrenceFrequency.FixedIntervalDays,
                    14,
                    RecurrenceMode.Dynamic,
                    null,
                    AssignmentPolicy.Both,
                    null,
                    new DateOnly(2026, 8, 21),
                    []),
            ],
            [new ImportChoreRoomMapping("Cucina", null, true)]);

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        result.Imported.Should().Be(2);
        result.Skipped.Should().Be(0);
        result.CompletionsImported.Should().Be(3);
        result.CompletionsSkipped.Should().Be(0);

        var chores = await db.Chores.AsNoTracking().OrderBy(chore => chore.Title).ToListAsync(TestContext.Current.CancellationToken);
        chores.Should().HaveCount(2);
        chores.Select(chore => chore.RoomId).Should().OnlyContain(roomId => roomId != null);

        var glasses = chores.Single(chore => chore.Title == "Pulire Vetri");
        glasses.LastCompletedDate.Should().Be(new DateOnly(2026, 9, 10));
        glasses.NextOccurrenceDate.Should().Be(new DateOnly(2026, 11, 9));
        glasses.FixedAssigneeMemberId.Should().Be(member.Id);

        var radiators = chores.Single(chore => chore.Title == "Pulire Termosifoni");
        radiators.LastCompletedDate.Should().Be(new DateOnly(2026, 8, 21));
        radiators.NextOccurrenceDate.Should().Be(new DateOnly(2026, 9, 4));

        var history = await db.CompletionHistory.AsNoTracking().ToListAsync(TestContext.Current.CancellationToken);
        history.Should().HaveCount(3);
        history.Should().OnlyContain(item => item.Source == CompletionSource.HomeAssistant);
        history.Should().OnlyContain(item => item.CompletedByMemberId == null && item.CompletedByUserId == null);
        history.Select(item => item.CompletedDate).Should().BeEquivalentTo(
        [
            new DateOnly(2026, 9, 1),
            new DateOnly(2026, 9, 10),
            new DateOnly(2026, 8, 21),
        ]);
    }

    [Fact]
    public async Task ReimportSkipsExistingTitlesAndTheirHistory()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var db = database.Context;
        var time = TestTimeProvider.At(new DateOnly(2026, 10, 20));
        var household = await TestSeed.HouseholdAsync(db, time);
        var handler = new ImportChoresHandler(db, new ChoreRecurrenceService(new RecurrenceCalculator()), time);
        var command = new ImportChoresCommand(
            household.Id,
            [
                new ImportChoreRow(
                    2,
                    "4",
                    "Pulire Vetri",
                    null,
                    null,
                    90,
                    true,
                    null,
                    false,
                    RecurrenceFrequency.Daily,
                    60,
                    RecurrenceMode.Dynamic,
                    null,
                    AssignmentPolicy.Both,
                    null,
                    null,
                    [new DateOnly(2026, 9, 1)]),
            ],
            []);

        var first = await handler.HandleAsync(command, TestContext.Current.CancellationToken);
        var second = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        first.Imported.Should().Be(1);
        second.Imported.Should().Be(0);
        second.Skipped.Should().Be(1);
        second.CompletionsImported.Should().Be(0);
        second.CompletionsSkipped.Should().Be(1);

        (await db.Chores.CountAsync(TestContext.Current.CancellationToken)).Should().Be(1);
        (await db.CompletionHistory.CountAsync(TestContext.Current.CancellationToken)).Should().Be(1);
    }

    [Fact]
    public async Task UnmappedRoomRejectsTheWholeBatch()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var db = database.Context;
        var time = TestTimeProvider.At(new DateOnly(2026, 10, 20));
        var household = await TestSeed.HouseholdAsync(db, time);
        var handler = new ImportChoresHandler(db, new ChoreRecurrenceService(new RecurrenceCalculator()), time);
        var command = new ImportChoresCommand(
            household.Id,
            [
                new ImportChoreRow(
                    2,
                    "4",
                    "Pulire Vetri",
                    null,
                    "Cucina",
                    90,
                    true,
                    null,
                    false,
                    RecurrenceFrequency.Daily,
                    1,
                    RecurrenceMode.Dynamic,
                    null,
                    AssignmentPolicy.Both,
                    null,
                    null,
                    []),
            ],
            []);

        var act = async () => await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ApplicationValidationException>().WithMessage("*no mapping*");
        (await db.Chores.CountAsync(TestContext.Current.CancellationToken)).Should().Be(0);
    }
}

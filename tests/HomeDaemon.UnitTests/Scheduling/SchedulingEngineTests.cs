using HomeDaemon.Application.Scheduling;

namespace HomeDaemon.UnitTests.Scheduling;

public sealed class SchedulingEngineTests
{
    private static readonly DateOnly Start = new(2026, 7, 1);
    private static readonly Guid Alice = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid Bob = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid Room = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

    [Fact]
    public void PlacesTheChoreAsCloseAsPossibleToTheDueDate()
    {
        var candidate = Candidate("00000000-0000-0000-0000-000000000001", Start.AddDays(1));

        var result = SchedulingEngine.Schedule(BuildInput([candidate]));

        result.Assignments.Should().ContainSingle();
        result.Assignments[0].Date.Should().Be(Start.AddDays(1));
    }

    [Fact]
    public void SameInput_ProducesTheSamePlan()
    {
        var candidates = new[]
        {
            Candidate("00000000-0000-0000-0000-000000000001", Start, 120),
            Candidate("00000000-0000-0000-0000-000000000002", Start, 120),
            Candidate("00000000-0000-0000-0000-000000000003", Start.AddDays(1), 90),
        };

        var first = SchedulingEngine.Schedule(BuildInput(candidates));
        var second = SchedulingEngine.Schedule(BuildInput(candidates));

        first.Assignments.Should().BeEquivalentTo(second.Assignments, options => options.WithStrictOrdering());
    }

    [Fact]
    public void SkipsDaysWithoutEnoughCapacity()
    {
        var candidate = Candidate("00000000-0000-0000-0000-000000000001", Start, 60);
        var input = BuildInput([candidate], capacity: new Dictionary<DateOnly, int>
        {
            [Start] = 10,
            [Start.AddDays(1)] = 480,
        });

        var result = SchedulingEngine.Schedule(input);

        result.Assignments[0].Date.Should().Be(Start.AddDays(1));
    }

    [Fact]
    public void ReportsChoresThatDoNotFit()
    {
        var candidate = Candidate("00000000-0000-0000-0000-000000000001", Start, 1000);

        var result = SchedulingEngine.Schedule(BuildInput([candidate]));

        result.Assignments.Should().BeEmpty();
        result.Unscheduled.Should().ContainSingle();
        result.Unscheduled[0].Reason.Should().Contain("enough free time");
    }

    [Fact]
    public void NeverSchedulesOnTheDayItWasJustCompleted()
    {
        var candidate = Candidate(
            "00000000-0000-0000-0000-000000000001",
            Start,
            lastCompleted: Start);
        var input = BuildInput([candidate], capacity: new Dictionary<DateOnly, int>
        {
            [Start] = 480,
            [Start.AddDays(1)] = 480,
        });

        var result = SchedulingEngine.Schedule(input);

        result.Assignments[0].Date.Should().Be(Start.AddDays(1));
    }

    [Fact]
    public void LockedChores_KeepTheirSlotAndReserveCapacity()
    {
        var locked = Candidate("00000000-0000-0000-0000-000000000001", Start, 300, locked: true, lockedDate: Start, lockedMember: Alice);
        var flexible = Candidate("00000000-0000-0000-0000-000000000002", Start, 400);
        var input = BuildInput([locked, flexible], defaultCapacity: 300);

        var result = SchedulingEngine.Schedule(input);

        result.Assignments.Should().Contain(assignment => assignment.ChoreId == locked.ChoreId && assignment.MemberId == Alice && assignment.Date == Start);
        result.Unscheduled.Should().Contain(item => item.ChoreId == flexible.ChoreId);
    }

    [Fact]
    public void DistributesLoadAcrossMembers()
    {
        var first = Candidate("00000000-0000-0000-0000-000000000001", Start, 300);
        var second = Candidate("00000000-0000-0000-0000-000000000002", Start, 300);
        var input = BuildInput([first, second], capacity: new Dictionary<DateOnly, int> { [Start] = 480 });

        var result = SchedulingEngine.Schedule(input);

        result.Assignments.Should().HaveCount(2);
        result.Assignments.Select(assignment => assignment.MemberId).Distinct().Should().HaveCount(2);
    }

    [Fact]
    public void SameRoomBonus_PullsAChoreTowardsItsRoom()
    {
        var anchor = Candidate("00000000-0000-0000-0000-000000000001", Start, 30, room: Room);
        var follower = Candidate("00000000-0000-0000-0000-000000000002", Start.AddDays(1), 30, room: Room, sameRoom: true);

        var withoutBonus = SchedulingEngine.Schedule(BuildInput([anchor, follower], members: [Alice], options: new SchedulingOptions { SameRoomBonus = 0 }));
        var withBonus = SchedulingEngine.Schedule(BuildInput([anchor, follower], members: [Alice], options: new SchedulingOptions { SameRoomBonus = 20 }));

        withoutBonus.Assignments.Single(assignment => assignment.ChoreId == follower.ChoreId).Date.Should().Be(Start.AddDays(1));
        withBonus.Assignments.Single(assignment => assignment.ChoreId == follower.ChoreId).Date.Should().Be(Start);
    }

    private static ChoreCandidate Candidate(
        string id,
        DateOnly due,
        int duration = 30,
        IReadOnlyList<Guid>? members = null,
        bool locked = false,
        DateOnly? lockedDate = null,
        Guid? lockedMember = null,
        Guid? room = null,
        bool sameRoom = false,
        DateOnly? lastCompleted = null) =>
        new(Guid.Parse(id), room, duration, due, locked, lockedDate, lockedMember, sameRoom, members ?? [Alice, Bob], lastCompleted);

    private static SchedulingInput BuildInput(
        IReadOnlyList<ChoreCandidate> candidates,
        IReadOnlyList<Guid>? members = null,
        Dictionary<DateOnly, int>? capacity = null,
        SchedulingOptions? options = null,
        int defaultCapacity = 480)
    {
        var memberIds = members ?? [Alice, Bob];
        var free = new Dictionary<(DateOnly Date, Guid MemberId), int>();
        for (var i = 0; i < 3; i++)
        {
            var date = Start.AddDays(i);
            var dayCapacity = capacity is not null && capacity.TryGetValue(date, out var value) ? value : defaultCapacity;
            foreach (var member in memberIds)
            {
                free[(date, member)] = dayCapacity;
            }
        }

        return new SchedulingInput(Start, Start.AddDays(2), candidates, free, options ?? new SchedulingOptions());
    }
}

using System.Text.Json;
using HomeDaemon.Application.Sync;
using HomeDaemon.Domain.Chores;
using HomeDaemon.Domain.Households;

namespace HomeDaemon.UnitTests.Sync;

public sealed class TodoSyncPlannerTests
{
    private static readonly Guid ChoreId = Guid.Parse("00000000-0000-0000-0000-0000000000aa");
    private static readonly Guid AliceId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid BobId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");
    private static readonly IReadOnlyDictionary<Guid, string> NoRooms = new Dictionary<Guid, string>();
    private const string ListA = "todo.alice";
    private const string ListB = "todo.bob";

    [Fact]
    public void ScheduledChoreWithoutLink_ProducesCreate()
    {
        var plan = TodoSyncPlanner.BuildPlan([Chore(AliceId)], [Member(AliceId, ListA)], NoRooms, []);

        plan.Should().ContainSingle();
        var operation = plan[0];
        operation.OperationType.Should().Be(Domain.Sync.TodoSyncOperationType.Create);
        operation.TodoListEntityId.Should().Be(ListA);
        operation.ItemUid.Should().BeNull();
    }

    [Fact]
    public void ScheduledChoreWithMatchingLink_ProducesUpdate()
    {
        var plan = TodoSyncPlanner.BuildPlan([Chore(AliceId)], [Member(AliceId, ListA)], NoRooms, [Link(ListA, "uid-1")]);

        plan.Should().ContainSingle();
        plan[0].OperationType.Should().Be(Domain.Sync.TodoSyncOperationType.Update);
        plan[0].ItemUid.Should().Be("uid-1");
    }

    [Fact]
    public void ChangedAssignee_ProducesDeleteThenCreate()
    {
        var members = new[] { Member(AliceId, ListA), Member(BobId, ListB) };

        var plan = TodoSyncPlanner.BuildPlan([Chore(BobId)], members, NoRooms, [Link(ListA, "uid-1")]);

        plan.Should().HaveCount(2);
        plan[0].OperationType.Should().Be(Domain.Sync.TodoSyncOperationType.Delete);
        plan[0].TodoListEntityId.Should().Be(ListA);
        plan[1].OperationType.Should().Be(Domain.Sync.TodoSyncOperationType.Create);
        plan[1].TodoListEntityId.Should().Be(ListB);
    }

    [Fact]
    public void PausedChoreWithLink_ProducesDelete()
    {
        var chore = Chore(AliceId);
        chore.IsActive = false;

        var plan = TodoSyncPlanner.BuildPlan([chore], [Member(AliceId, ListA)], NoRooms, [Link(ListA, "uid-1")]);

        plan.Should().ContainSingle();
        plan[0].OperationType.Should().Be(Domain.Sync.TodoSyncOperationType.Delete);
    }

    [Fact]
    public void Payload_UsesRoomAndDurationInTheSummary()
    {
        var chore = Chore(AliceId);
        var rooms = new Dictionary<Guid, string> { [chore.RoomId!.Value] = "Kitchen" };

        var plan = TodoSyncPlanner.BuildPlan([chore], [Member(AliceId, ListA)], rooms, []);
        var payload = JsonSerializer.Deserialize<TodoSyncPayload>(plan[0].Payload);

        payload.Should().NotBeNull();
        payload!.Summary.Should().Be("[Kitchen] Clean oven (30 min)");
        payload.DueDate.Should().Be(chore.NextDueDate);
    }

    private static ChoreDefinition Chore(Guid plannedMemberId) => new()
    {
        Id = ChoreId,
        Title = "Clean oven",
        EstimatedMinutes = 30,
        RoomId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
        IsActive = true,
        ScheduleState = ChoreScheduleState.Scheduled,
        NextDueDate = new DateOnly(2026, 8, 1),
        PlannedMemberId = plannedMemberId,
    };

    private static HouseholdMember Member(Guid id, string todoListEntityId) => new()
    {
        Id = id,
        IsAssignable = true,
        TodoListEntityId = todoListEntityId,
    };

    private static TodoItemLink Link(string listEntityId, string uid) => new()
    {
        ChoreDefinitionId = ChoreId,
        TodoListEntityId = listEntityId,
        ItemUid = uid,
    };
}

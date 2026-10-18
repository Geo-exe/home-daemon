using System.Globalization;
using System.Text.Json;
using HomeDaemon.Domain.Chores;
using HomeDaemon.Domain.Households;
using HomeDaemon.Domain.Sync;

namespace HomeDaemon.Application.Sync;

/// <summary>
/// Pure diff between the desired plan and the existing Home Assistant links. It only
/// ever touches items that HomeDaemon itself mapped; foreign to-do items are untouched.
/// </summary>
public static class TodoSyncPlanner
{
    public static IReadOnlyList<TodoSyncOperation> BuildPlan(
        IReadOnlyList<ChoreDefinition> chores,
        IReadOnlyList<HouseholdMember> members,
        IReadOnlyDictionary<Guid, string> roomNames,
        IReadOnlyList<TodoItemLink> links)
    {
        ArgumentNullException.ThrowIfNull(chores);
        ArgumentNullException.ThrowIfNull(members);
        ArgumentNullException.ThrowIfNull(roomNames);
        ArgumentNullException.ThrowIfNull(links);

        var membersById = members.ToDictionary(member => member.Id);
        var linkByChore = new Dictionary<Guid, TodoItemLink>();
        var orderedLinks = links.OrderBy(item => item.UpdatedAtUtc).ToList();
        foreach (var link in orderedLinks)
        {
            linkByChore[link.ChoreDefinitionId] = link;
        }

        var operations = new List<TodoSyncOperation>();
        foreach (var chore in chores)
        {
            var hasLink = linkByChore.TryGetValue(chore.Id, out var link);
            var desired = ResolveDesired(chore, membersById, roomNames);

            if (desired is null)
            {
                if (hasLink)
                {
                    operations.Add(new TodoSyncOperation(chore.Id, TodoSyncOperationType.Delete, link!.TodoListEntityId, link.ItemUid, "{}"));
                }

                continue;
            }

            var (listEntityId, payload) = desired.Value;
            if (!hasLink)
            {
                operations.Add(new TodoSyncOperation(chore.Id, TodoSyncOperationType.Create, listEntityId, null, payload));
            }
            else if (!string.Equals(link!.TodoListEntityId, listEntityId, StringComparison.Ordinal))
            {
                operations.Add(new TodoSyncOperation(chore.Id, TodoSyncOperationType.Delete, link.TodoListEntityId, link.ItemUid, "{}"));
                operations.Add(new TodoSyncOperation(chore.Id, TodoSyncOperationType.Create, listEntityId, null, payload));
            }
            else
            {
                operations.Add(new TodoSyncOperation(chore.Id, TodoSyncOperationType.Update, listEntityId, link.ItemUid, payload));
            }
        }

        return operations;
    }

    public static string BuildSummary(string title, string? roomName, int estimatedMinutes)
    {
        var minutes = estimatedMinutes.ToString(CultureInfo.InvariantCulture);
        return roomName is null
            ? $"{title} ({minutes} min)"
            : $"[{roomName}] {title} ({minutes} min)";
    }

    private static (string ListEntityId, string Payload)? ResolveDesired(
        ChoreDefinition chore,
        Dictionary<Guid, HouseholdMember> membersById,
        IReadOnlyDictionary<Guid, string> roomNames)
    {
        if (!chore.IsActive
            || chore.ScheduleState != ChoreScheduleState.Scheduled
            || chore.NextDueDate is null
            || chore.PlannedMemberId is not Guid memberId
            || !membersById.TryGetValue(memberId, out var member)
            || string.IsNullOrWhiteSpace(member.TodoListEntityId))
        {
            return null;
        }

        var roomName = chore.RoomId is Guid roomId && roomNames.TryGetValue(roomId, out var name) ? name : null;
        var payload = JsonSerializer.Serialize(new TodoSyncPayload(
            BuildSummary(chore.Title, roomName, chore.EstimatedMinutes),
            chore.Description,
            chore.NextDueDate));

        return (member.TodoListEntityId, payload);
    }
}

namespace HomeDaemon.Application.Scheduling;

/// <summary>
/// Deterministic load-balancing scheduler. Chores are placed one at a time, honouring hard
/// constraints and minimizing a documented score. Ties are broken by day and then member id
/// so that two identical recalculation passes never shuffle tasks.
/// </summary>
public static class SchedulingEngine
{
    private const double _scoreEpsilon = 1e-9;

    public static SchedulingResult Schedule(SchedulingInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var freeMinutes = new Dictionary<(DateOnly Date, Guid MemberId), int>(input.FreeMinutes);
        var initialFreeMinutes = new Dictionary<(DateOnly Date, Guid MemberId), int>(input.FreeMinutes);
        var assignments = new List<ChoreAssignment>();
        var unscheduled = new List<UnscheduledChore>();
        var assignedMinutesByMember = new Dictionary<Guid, int>();
        var roomsByDay = new Dictionary<DateOnly, HashSet<Guid>>();

        var ordered = input.Candidates
            .OrderBy(static candidate => candidate.IsLocked ? 0 : 1)
            .ThenBy(static candidate => candidate.DueDate)
            .ThenByDescending(static candidate => candidate.DurationMinutes)
            .ThenBy(static candidate => candidate.ChoreId)
            .ToList();

        foreach (var candidate in ordered)
        {
            if (candidate.IsLocked)
            {
                Apply(
                    candidate,
                    (candidate.LockedDate!.Value, candidate.LockedMemberId!.Value),
                    freeMinutes,
                    assignments,
                    assignedMinutesByMember,
                    roomsByDay);
                continue;
            }

            ScheduleCandidate(
                candidate,
                input,
                freeMinutes,
                initialFreeMinutes,
                assignments,
                unscheduled,
                assignedMinutesByMember,
                roomsByDay);
        }

        return new SchedulingResult(assignments, unscheduled, freeMinutes);
    }

    private static void ScheduleCandidate(
        ChoreCandidate candidate,
        SchedulingInput input,
        Dictionary<(DateOnly Date, Guid MemberId), int> freeMinutes,
        Dictionary<(DateOnly Date, Guid MemberId), int> initialFreeMinutes,
        List<ChoreAssignment> assignments,
        List<UnscheduledChore> unscheduled,
        Dictionary<Guid, int> assignedMinutesByMember,
        Dictionary<DateOnly, HashSet<Guid>> roomsByDay)
    {
        (DateOnly Date, Guid MemberId)? best = null;
        var bestScore = double.MaxValue;

        for (var day = input.ReferenceDate; day <= input.HorizonEndDate; day = day.AddDays(1))
        {
            if (candidate.LastCompletedDate == day)
            {
                continue;
            }

            foreach (var member in candidate.AllowedMemberIds)
            {
                if (!freeMinutes.TryGetValue((day, member), out var availableFree))
                {
                    continue;
                }

                if (availableFree < candidate.DurationMinutes)
                {
                    continue;
                }

                var score = Score(candidate, day, member, availableFree, initialFreeMinutes, assignedMinutesByMember, roomsByDay, input.Options);
                var isBetter = best is null
                    || score < bestScore - _scoreEpsilon
                    || (Math.Abs(score - bestScore) <= _scoreEpsilon && IsPreferred(day, member, best.Value));

                if (isBetter)
                {
                    best = (day, member);
                    bestScore = score;
                }
            }
        }

        if (best is null)
        {
            unscheduled.Add(new UnscheduledChore(candidate.ChoreId, BuildUnscheduledReason(candidate, input, freeMinutes)));
            return;
        }

        Apply(candidate, best.Value, freeMinutes, assignments, assignedMinutesByMember, roomsByDay);
    }

    private static double Score(
        ChoreCandidate candidate,
        DateOnly day,
        Guid member,
        int availableFree,
        Dictionary<(DateOnly Date, Guid MemberId), int> initialFreeMinutes,
        Dictionary<Guid, int> assignedMinutesByMember,
        Dictionary<DateOnly, HashSet<Guid>> roomsByDay,
        SchedulingOptions options)
    {
        var distance = Math.Abs(day.DayNumber - candidate.DueDate.DayNumber);
        var score = options.DueDateDistanceWeight * distance;
        if (day > candidate.DueDate)
        {
            score += options.LatePenaltyWeight * (day.DayNumber - candidate.DueDate.DayNumber);
        }

        var initial = initialFreeMinutes.TryGetValue((day, member), out var value) ? value : Math.Max(1, availableFree);
        var remainingRatio = 1.0 - ((double)(availableFree - candidate.DurationMinutes) / Math.Max(1, initial));
        score += options.RemainingCapacityWeight * remainingRatio;

        var memberLoad = assignedMinutesByMember.TryGetValue(member, out var minutes) ? minutes : 0;
        score += options.LoadBalanceWeight * (memberLoad / 60.0);

        if (candidate.PreferSameRoomGrouping
            && candidate.RoomId is Guid roomId
            && roomsByDay.TryGetValue(day, out var rooms)
            && rooms.Contains(roomId))
        {
            score -= options.SameRoomBonus;
        }

        return score;
    }

    private static bool IsPreferred(DateOnly day, Guid member, (DateOnly Date, Guid MemberId) current) =>
        day < current.Date || (day == current.Date && member.CompareTo(current.MemberId) < 0);

    private static void Apply(
        ChoreCandidate candidate,
        (DateOnly Date, Guid MemberId) slot,
        Dictionary<(DateOnly Date, Guid MemberId), int> freeMinutes,
        List<ChoreAssignment> assignments,
        Dictionary<Guid, int> assignedMinutesByMember,
        Dictionary<DateOnly, HashSet<Guid>> roomsByDay)
    {
        var key = (slot.Date, slot.MemberId);
        var current = freeMinutes.TryGetValue(key, out var value) ? value : 0;
        freeMinutes[key] = Math.Max(0, current - candidate.DurationMinutes);

        assignments.Add(new ChoreAssignment(candidate.ChoreId, slot.Date, slot.MemberId));

        assignedMinutesByMember[slot.MemberId] =
            (assignedMinutesByMember.TryGetValue(slot.MemberId, out var minutes) ? minutes : 0) + candidate.DurationMinutes;

        if (candidate.RoomId is Guid room)
        {
            if (!roomsByDay.TryGetValue(slot.Date, out var rooms))
            {
                rooms = [];
                roomsByDay[slot.Date] = rooms;
            }

            rooms.Add(room);
        }
    }

    private static string BuildUnscheduledReason(
        ChoreCandidate candidate,
        SchedulingInput input,
        Dictionary<(DateOnly Date, Guid MemberId), int> freeMinutes)
    {
        var maxFree = 0;
        for (var day = input.ReferenceDate; day <= input.HorizonEndDate; day = day.AddDays(1))
        {
            foreach (var member in candidate.AllowedMemberIds)
            {
                if (freeMinutes.TryGetValue((day, member), out var value) && value > maxFree)
                {
                    maxFree = value;
                }
            }
        }

        return maxFree < candidate.DurationMinutes
            ? "No day within the horizon has enough free time."
            : "No valid placement was found within the horizon.";
    }
}

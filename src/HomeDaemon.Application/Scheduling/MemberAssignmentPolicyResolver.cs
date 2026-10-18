using HomeDaemon.Domain.Chores;
using HomeDaemon.Domain.Households;

namespace HomeDaemon.Application.Scheduling;

/// <summary>
/// Resolves which members may be assigned to a chore according to its assignment policy.
/// Alternation uses the last completer as the reference so work genuinely rotates.
/// </summary>
public static class MemberAssignmentPolicyResolver
{
    public static IReadOnlyList<Guid> Resolve(ChoreDefinition chore, IReadOnlyList<HouseholdMember> members)
    {
        ArgumentNullException.ThrowIfNull(chore);
        ArgumentNullException.ThrowIfNull(members);

        var assignable = new List<Guid>();
        foreach (var member in members)
        {
            if (member.IsAssignable)
            {
                assignable.Add(member.Id);
            }
        }

        switch (chore.AssignmentPolicy)
        {
            case AssignmentPolicy.SpecificMember:
                return chore.FixedAssigneeMemberId is Guid fixedId && assignable.Contains(fixedId)
                    ? [fixedId]
                    : [];

            case AssignmentPolicy.Alternating:
                var alternates = assignable.Where(id => id != chore.LastCompletedByMemberId).ToList();
                return alternates.Count > 0 ? alternates : assignable;

            default:
                return assignable;
        }
    }
}

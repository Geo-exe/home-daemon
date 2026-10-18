namespace HomeDaemon.Domain.Chores;

/// <summary>Which members the scheduler may pick for a chore.</summary>
public enum AssignmentPolicy
{
    /// <summary>Any assignable member.</summary>
    Both = 0,

    /// <summary>Only <c>ChoreDefinition.FixedAssigneeMemberId</c>.</summary>
    SpecificMember = 1,

    /// <summary>Strict alternation: whoever did not complete the previous cycle.</summary>
    Alternating = 2,
}

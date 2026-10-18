namespace HomeDaemon.Domain.Chores;

/// <summary>
/// How the next theoretical occurrence is derived. <see cref="Dynamic"/> rolls the cycle
/// forward from the actual completion date; <see cref="Anchored"/> keeps a fixed anchor so
/// execution delays do not shift the whole series.
/// </summary>
public enum RecurrenceMode
{
    Dynamic = 0,
    Anchored = 1,
}

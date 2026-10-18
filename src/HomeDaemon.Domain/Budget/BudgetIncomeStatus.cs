namespace HomeDaemon.Domain.Budget;

/// <summary>
/// Works out which months of a settlement year still have no income recorded, so the caller can
/// tell the user the balance is provisional. A person added mid-year is only expected to have
/// income from the month they were created.
/// </summary>
public static class BudgetIncomeStatus
{
    /// <summary>
    /// Returns the expected months (inclusive range <paramref name="fromMonth"/>..
    /// <paramref name="throughMonth"/>) that are missing from <paramref name="recordedMonths"/>.
    /// </summary>
    public static IReadOnlyList<int> FindMissingMonths(
        IEnumerable<int> recordedMonths,
        int fromMonth,
        int throughMonth)
    {
        ArgumentNullException.ThrowIfNull(recordedMonths);

        var first = Math.Clamp(fromMonth, 1, 12);
        var last = Math.Clamp(throughMonth, 0, 12);
        var result = new List<int>();

        if (last < first)
        {
            return result;
        }

        var recorded = new HashSet<int>(recordedMonths);
        for (var month = first; month <= last; month++)
        {
            if (!recorded.Contains(month))
            {
                result.Add(month);
            }
        }

        return result;
    }
}

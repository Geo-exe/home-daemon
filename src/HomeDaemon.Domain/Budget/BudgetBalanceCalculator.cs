namespace HomeDaemon.Domain.Budget;

/// <summary>
/// Pure, deterministic balance maths for one settlement year. It never persists anything: the
/// proportion is annual and retroactive, so every read recomputes it from the raw amounts.
/// A positive balance means the person is owed money; a negative one means they owe.
/// </summary>
public static class BudgetBalanceCalculator
{
    public static BudgetBalance Calculate(
        IReadOnlyList<Guid> memberIds,
        IReadOnlyDictionary<Guid, long> annualIncomeMinor,
        IReadOnlyList<BudgetExpenseLine> expenses,
        IReadOnlyList<BudgetTransferLine> transfers)
    {
        ArgumentNullException.ThrowIfNull(memberIds);
        ArgumentNullException.ThrowIfNull(annualIncomeMinor);
        ArgumentNullException.ThrowIfNull(expenses);
        ArgumentNullException.ThrowIfNull(transfers);

        var totalIncome = 0L;
        foreach (var memberId in memberIds)
        {
            totalIncome += IncomeOf(annualIncomeMinor, memberId);
        }

        var totalExpenses = 0L;
        var paid = new Dictionary<Guid, long>();
        foreach (var expense in expenses)
        {
            totalExpenses += expense.AmountMinor;
            paid[expense.PaidByMemberId] = paid.GetValueOrDefault(expense.PaidByMemberId) + expense.AmountMinor;
        }

        var transferredOut = new Dictionary<Guid, long>();
        var transferredIn = new Dictionary<Guid, long>();
        foreach (var transfer in transfers)
        {
            transferredOut[transfer.FromMemberId] = transferredOut.GetValueOrDefault(transfer.FromMemberId) + transfer.AmountMinor;
            transferredIn[transfer.ToMemberId] = transferredIn.GetValueOrDefault(transfer.ToMemberId) + transfer.AmountMinor;
        }

        var proportionAvailable = totalIncome > 0;
        var dues = proportionAvailable
            ? DistributeDues(memberIds, annualIncomeMinor, totalIncome, totalExpenses)
            : null;

        var members = new List<BudgetMemberBalance>(memberIds.Count);
        foreach (var memberId in memberIds)
        {
            var income = IncomeOf(annualIncomeMinor, memberId);
            var memberPaid = paid.GetValueOrDefault(memberId);
            var outMinor = transferredOut.GetValueOrDefault(memberId);
            var inMinor = transferredIn.GetValueOrDefault(memberId);

            decimal? share = proportionAvailable ? (decimal)income / totalIncome : null;
            long? due = dues?.GetValueOrDefault(memberId);
            long? balance = due is long dueValue
                ? memberPaid - dueValue + outMinor - inMinor
                : null;

            members.Add(new BudgetMemberBalance(memberId, income, share, memberPaid, due, outMinor, inMinor, balance));
        }

        return new BudgetBalance(totalIncome, totalExpenses, proportionAvailable, members);
    }

    private static long IncomeOf(IReadOnlyDictionary<Guid, long> annualIncomeMinor, Guid memberId) =>
        Math.Max(0, annualIncomeMinor.GetValueOrDefault(memberId));

    /// <summary>
    /// Splits the total expense in proportion to each income using the largest-remainder method,
    /// so the due amounts always add up to the total to the cent.
    /// </summary>
    private static Dictionary<Guid, long> DistributeDues(
        IReadOnlyList<Guid> memberIds,
        IReadOnlyDictionary<Guid, long> annualIncomeMinor,
        long totalIncome,
        long totalExpenses)
    {
        var dues = new Dictionary<Guid, long>();
        var remainders = new List<(Guid MemberId, long Remainder)>();
        var assigned = 0L;

        foreach (var memberId in memberIds)
        {
            var income = IncomeOf(annualIncomeMinor, memberId);

            // Realistic domestic magnitudes keep this product far below long.MaxValue.
            var numerator = totalExpenses * income;
            var baseDue = numerator / totalIncome;
            var remainder = numerator % totalIncome;

            dues[memberId] = baseDue;
            assigned += baseDue;

            if (remainder > 0)
            {
                remainders.Add((memberId, remainder));
            }
        }

        var leftover = totalExpenses - assigned;
        if (leftover > 0)
        {
            // Largest remainder first; the member id breaks ties so the result is deterministic.
            remainders.Sort((left, right) => left.Remainder != right.Remainder
                ? right.Remainder.CompareTo(left.Remainder)
                : left.MemberId.CompareTo(right.MemberId));

            for (var index = 0; index < leftover && index < remainders.Count; index++)
            {
                dues[remainders[index].MemberId] += 1;
            }
        }

        return dues;
    }
}

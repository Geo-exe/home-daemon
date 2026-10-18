namespace HomeDaemon.Domain.Budget;

/// <summary>A transfer reduced to the two participants and the amount, for the balance.</summary>
public sealed record BudgetTransferLine(Guid FromMemberId, Guid ToMemberId, long AmountMinor);

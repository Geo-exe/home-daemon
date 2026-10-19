namespace HomeDaemon.Application.Budget;

/// <summary>One recent direction of money between two budget participants.</summary>
public sealed record BudgetTransferSuggestionDto(
    Guid FromMemberId,
    string FromMemberName,
    Guid ToMemberId,
    string ToMemberName);

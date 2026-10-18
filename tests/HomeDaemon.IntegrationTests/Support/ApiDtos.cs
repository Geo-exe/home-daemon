namespace HomeDaemon.IntegrationTests.Support;

/// <summary>Client-side mirrors of the API contracts used by the end-to-end tests.</summary>
internal sealed record CsrfTokenDto(string Token);

internal sealed record CurrentUserDto(
    Guid Id,
    string Email,
    string? DisplayName,
    Guid? HouseholdMemberId,
    bool MustChangePassword,
    IReadOnlyList<string> Roles);

internal sealed record UserDto(
    Guid Id,
    string Email,
    string? DisplayName,
    bool IsActive,
    bool MustChangePassword,
    Guid? HouseholdMemberId,
    IReadOnlyList<string> Roles);

internal sealed record TemporaryPasswordDto(UserDto User, string TemporaryPassword);

internal sealed record MemberDto(Guid Id, string Name, bool IsAssignable);

internal sealed record ChoreDto(Guid Id, string Title, bool IsActive);

internal sealed record HistoryItemDto(
    Guid Id,
    Guid ChoreDefinitionId,
    string ChoreTitle,
    DateOnly CompletedDate,
    DateTimeOffset RecordedAtUtc,
    string Source,
    Guid? CompletedByMemberId,
    string? CompletedByMemberName,
    Guid? CompletedByUserId);

internal sealed record HistoryPageDto(
    IReadOnlyList<HistoryItemDto> Items,
    string? NextCursor,
    bool HasMore);

internal sealed record BudgetCategoryResponse(Guid Id, string Name, int SortOrder);

internal sealed record BudgetExpenseResponse(
    Guid Id,
    DateOnly Date,
    long AmountMinor,
    Guid CategoryId,
    string CategoryName,
    Guid PaidByMemberId,
    string PaidByMemberName);

internal sealed record BudgetMemberSummaryResponse(
    Guid MemberId,
    string Name,
    long AnnualIncomeMinor,
    decimal? Share,
    long PaidMinor,
    long? DueMinor,
    long TransferredOutMinor,
    long TransferredInMinor,
    long? BalanceMinor,
    IReadOnlyList<int> MissingMonths);

internal sealed record BudgetCategoryTotalResponse(Guid CategoryId, string Name, long AmountMinor);

internal sealed record BudgetSummaryResponse(
    int Year,
    string Currency,
    long TotalIncomeMinor,
    long TotalExpensesMinor,
    bool ProportionAvailable,
    bool IncomeComplete,
    IReadOnlyList<BudgetMemberSummaryResponse> Members,
    IReadOnlyList<BudgetCategoryTotalResponse> ByCategory);

using HomeDaemon.Domain.Budget;
using HomeDaemon.Domain.Households;

namespace HomeDaemon.Application.Budget;

internal static class BudgetMappings
{
    public static BudgetSubcategoryDto ToDto(BudgetSubcategory subcategory) =>
        new(subcategory.Id, subcategory.CategoryId, subcategory.Name, subcategory.SortOrder);

    public static BudgetCategoryDto ToDto(BudgetCategory category, IReadOnlyList<BudgetSubcategoryDto> subcategories) =>
        new(category.Id, category.Name, category.SortOrder, subcategories);

    public static BudgetIncomeMonthDto ToDto(BudgetIncome income) =>
        new(income.Month, income.AmountMinor, income.Id);

    public static BudgetExpenseDto ToDto(
        BudgetExpense expense,
        string categoryName,
        string? subcategoryName,
        string paidByMemberName) =>
        new(
            expense.Id,
            expense.Date,
            expense.AmountMinor,
            expense.CategoryId,
            categoryName,
            expense.SubcategoryId,
            subcategoryName,
            expense.PaidByMemberId,
            paidByMemberName,
            expense.Description);

    public static BudgetTransferDto ToDto(BudgetTransfer transfer, string fromMemberName, string toMemberName) =>
        new(
            transfer.Id,
            transfer.Date,
            transfer.SettlementYear,
            transfer.AmountMinor,
            transfer.FromMemberId,
            fromMemberName,
            transfer.ToMemberId,
            toMemberName,
            transfer.Description);
}

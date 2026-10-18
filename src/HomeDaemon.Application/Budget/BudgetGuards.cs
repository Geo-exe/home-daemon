using HomeDaemon.Application.Abstractions;
using HomeDaemon.Application.Common;
using HomeDaemon.Domain.Budget;
using HomeDaemon.Domain.Households;
using Microsoft.EntityFrameworkCore;

namespace HomeDaemon.Application.Budget;

/// <summary>
/// Validation shared by the budget commands. Every rule here is authoritative: the frontend
/// mirrors it only to surface mistakes early.
/// </summary>
internal static class BudgetGuards
{
    public const int MinYear = 2000;
    public const int MaxYear = 2100;

    public static string RequireName(string? name, string field)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ApplicationValidationException($"The {field} name is required.");
        }

        var trimmed = name.Trim();
        if (trimmed.Length > 200)
        {
            throw new ApplicationValidationException($"The {field} name must be 200 characters or fewer.");
        }

        return trimmed;
    }

    public static void RequirePositive(long amountMinor, string what)
    {
        if (amountMinor <= 0)
        {
            throw new ApplicationValidationException($"The {what} amount must be greater than zero.");
        }
    }

    public static void RequireNonNegative(long amountMinor, string what)
    {
        if (amountMinor < 0)
        {
            throw new ApplicationValidationException($"The {what} amount must not be negative.");
        }
    }

    public static void RequireYear(int year)
    {
        if (year is < MinYear or > MaxYear)
        {
            throw new ApplicationValidationException($"The year must be between {MinYear} and {MaxYear}.");
        }
    }

    public static void RequireMonth(int month)
    {
        if (month is < 1 or > 12)
        {
            throw new ApplicationValidationException("The month must be between 1 and 12.");
        }
    }

    public static async Task<BudgetCategory> RequireCategoryAsync(
        IApplicationDbContext db,
        Guid householdId,
        Guid categoryId,
        CancellationToken cancellationToken)
    {
        return await db.BudgetCategories
            .AsNoTracking()
            .FirstOrDefaultAsync(category => category.Id == categoryId && category.HouseholdId == householdId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new EntityNotFoundException($"Category '{categoryId}' was not found.");
    }

    public static async Task<BudgetSubcategory> RequireSubcategoryAsync(
        IApplicationDbContext db,
        Guid householdId,
        Guid categoryId,
        Guid subcategoryId,
        CancellationToken cancellationToken)
    {
        var subcategory = await db.BudgetSubcategories
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == subcategoryId && item.HouseholdId == householdId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new EntityNotFoundException($"Subcategory '{subcategoryId}' was not found.");

        if (subcategory.CategoryId != categoryId)
        {
            throw new ApplicationValidationException("The subcategory does not belong to the selected category.");
        }

        return subcategory;
    }

    public static async Task<HouseholdMember> RequireParticipantAsync(
        IApplicationDbContext db,
        Guid householdId,
        Guid memberId,
        CancellationToken cancellationToken)
    {
        var member = await db.HouseholdMembers
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == memberId && item.HouseholdId == householdId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new EntityNotFoundException($"Member '{memberId}' was not found.");

        if (!member.IsBudgetParticipant)
        {
            throw new ApplicationValidationException("The selected person does not take part in the budget.");
        }

        return member;
    }
}

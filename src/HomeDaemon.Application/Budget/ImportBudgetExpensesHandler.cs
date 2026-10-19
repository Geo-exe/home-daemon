using System.Globalization;
using HomeDaemon.Application.Abstractions;
using HomeDaemon.Application.Common;
using HomeDaemon.Domain.Budget;
using Microsoft.EntityFrameworkCore;

namespace HomeDaemon.Application.Budget;

/// <summary>
/// Imports a batch of expenses in one unit of work. Categories, subcategories and expenses are
/// written together, so a bad row never leaves half of the file in the database.
/// </summary>
public sealed class ImportBudgetExpensesHandler(IApplicationDbContext db, TimeProvider timeProvider)
{
    public const int MaxRows = 10_000;

    public async Task<ImportBudgetExpensesResult> HandleAsync(
        ImportBudgetExpensesCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        RequireBatchSize(command);

        var now = timeProvider.GetUtcNow();
        var existingCategories = await db.BudgetCategories
            .Where(category => category.HouseholdId == command.HouseholdId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var categories = ResolveCategories(command, existingCategories, now);

        var existingSubcategories = await db.BudgetSubcategories
            .Where(subcategory => subcategory.HouseholdId == command.HouseholdId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var subcategories = ResolveSubcategories(command, categories, existingSubcategories, now);

        var participants = await ResolveParticipantsAsync(command, cancellationToken).ConfigureAwait(false);
        var existingExpenses = await ExistingFingerprintsAsync(command, cancellationToken).ConfigureAwait(false);
        var (expenses, skipped) = BuildExpenses(command, categories, subcategories, participants, existingExpenses, now);

        db.BudgetExpenses.AddRange(expenses);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new ImportBudgetExpensesResult(expenses.Count, skipped);
    }

    private static void RequireBatchSize(ImportBudgetExpensesCommand command)
    {
        if (command.Rows.Count == 0)
        {
            throw new ApplicationValidationException("The file does not contain any expense.");
        }

        if (command.Rows.Count > MaxRows)
        {
            throw new ApplicationValidationException(
                string.Create(CultureInfo.InvariantCulture, $"A single import can contain at most {MaxRows} expenses."));
        }
    }

    private Dictionary<string, BudgetCategory> ResolveCategories(
        ImportBudgetExpensesCommand command,
        List<BudgetCategory> existing,
        DateTimeOffset now)
    {
        var byName = new Dictionary<string, BudgetCategory>(StringComparer.OrdinalIgnoreCase);
        var byId = new Dictionary<Guid, BudgetCategory>();

        foreach (var category in existing)
        {
            byName[NormalizeKey(category.Name)] = category;
            byId[category.Id] = category;
        }

        var resolved = new Dictionary<string, BudgetCategory>(StringComparer.OrdinalIgnoreCase);
        var nextSortOrder = existing.Count == 0 ? 0 : existing.Max(category => category.SortOrder) + 1;

        foreach (var mapping in command.Categories)
        {
            var source = RequireSource(mapping.SourceName, "category");
            var willCreate = mapping.Create && !byName.ContainsKey(source);

            if (!resolved.TryAdd(
                source,
                ResolveCategory(mapping, command.HouseholdId, byName, byId, nextSortOrder, now)))
            {
                throw new ApplicationValidationException(
                    $"The category '{mapping.SourceName}' is mapped more than once.");
            }

            if (willCreate)
            {
                nextSortOrder++;
            }
        }

        return resolved;
    }

    private BudgetCategory ResolveCategory(
        ImportCategoryMapping mapping,
        Guid householdId,
        Dictionary<string, BudgetCategory> byName,
        Dictionary<Guid, BudgetCategory> byId,
        int sortOrder,
        DateTimeOffset now)
    {
        var source = NormalizeKey(mapping.SourceName);
        if (mapping.Create && mapping.CategoryId is not null)
        {
            throw new ApplicationValidationException(
                $"The category '{mapping.SourceName}' cannot be both mapped and created.");
        }

        if (mapping.Create)
        {
            // A category that appeared since the preview is reused instead of rejected, which
            // keeps re-importing the same file safe.
            if (byName.TryGetValue(source, out var existing))
            {
                return existing;
            }

            var name = BudgetGuards.RequireName(mapping.SourceName, "category");
            var created = new BudgetCategory
            {
                HouseholdId = householdId,
                Name = name,
                SortOrder = sortOrder,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
            };
            db.BudgetCategories.Add(created);
            byName[source] = created;
            return created;
        }

        if (mapping.CategoryId is not Guid categoryId
            || !byId.TryGetValue(categoryId, out var category))
        {
            throw new ApplicationValidationException(
                $"The category '{mapping.SourceName}' must be mapped to an existing category.");
        }

        return category;
    }

    private Dictionary<string, BudgetSubcategory> ResolveSubcategories(
        ImportBudgetExpensesCommand command,
        Dictionary<string, BudgetCategory> categories,
        List<BudgetSubcategory> existing,
        DateTimeOffset now)
    {
        var byId = existing.ToDictionary(subcategory => subcategory.Id);
        var byName = new Dictionary<string, BudgetSubcategory>(StringComparer.OrdinalIgnoreCase);
        var nextSortOrder = new Dictionary<Guid, int>();

        foreach (var subcategory in existing)
        {
            byName[SubcategoryNameKey(subcategory.CategoryId, subcategory.Name)] = subcategory;
            nextSortOrder[subcategory.CategoryId] = Math.Max(
                nextSortOrder.GetValueOrDefault(subcategory.CategoryId),
                subcategory.SortOrder + 1);
        }

        var resolved = new Dictionary<string, BudgetSubcategory>(StringComparer.OrdinalIgnoreCase);

        foreach (var mapping in command.Subcategories)
        {
            _ = RequireSource(mapping.SourceName, "subcategory");
            var categorySource = RequireSource(mapping.CategorySourceName, "category");

            if (!categories.TryGetValue(categorySource, out var category))
            {
                throw new ApplicationValidationException(
                    $"The subcategory '{mapping.SourceName}' references the unknown category '{mapping.CategorySourceName}'.");
            }

            if (!resolved.TryAdd(
                SubcategoryKey(mapping.CategorySourceName, mapping.SourceName),
                ResolveSubcategory(mapping, category, byId, byName, nextSortOrder, now)))
            {
                throw new ApplicationValidationException(
                    $"The subcategory '{mapping.SourceName}' is mapped more than once.");
            }
        }

        return resolved;
    }

    private BudgetSubcategory ResolveSubcategory(
        ImportSubcategoryMapping mapping,
        BudgetCategory category,
        Dictionary<Guid, BudgetSubcategory> byId,
        Dictionary<string, BudgetSubcategory> byName,
        Dictionary<Guid, int> nextSortOrder,
        DateTimeOffset now)
    {
        var source = NormalizeKey(mapping.SourceName);
        var nameKey = SubcategoryNameKey(category.Id, source);

        if (mapping.Create && mapping.SubcategoryId is not null)
        {
            throw new ApplicationValidationException(
                $"The subcategory '{mapping.SourceName}' cannot be both mapped and created.");
        }

        if (mapping.Create)
        {
            if (byName.TryGetValue(nameKey, out var existing))
            {
                return existing;
            }

            var name = BudgetGuards.RequireName(mapping.SourceName, "subcategory");
            var sortOrder = nextSortOrder.GetValueOrDefault(category.Id);
            var created = new BudgetSubcategory
            {
                HouseholdId = category.HouseholdId,
                CategoryId = category.Id,
                Name = name,
                SortOrder = sortOrder,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
            };
            db.BudgetSubcategories.Add(created);
            nextSortOrder[category.Id] = sortOrder + 1;
            byName[nameKey] = created;
            return created;
        }

        if (mapping.SubcategoryId is not Guid subcategoryId
            || !byId.TryGetValue(subcategoryId, out var subcategory))
        {
            throw new ApplicationValidationException(
                $"The subcategory '{mapping.SourceName}' must be mapped to an existing subcategory.");
        }

        if (subcategory.CategoryId != category.Id)
        {
            throw new ApplicationValidationException(
                $"The subcategory '{subcategory.Name}' does not belong to the category '{category.Name}'.");
        }

        return subcategory;
    }

    private async Task<Dictionary<Guid, bool>> ResolveParticipantsAsync(
        ImportBudgetExpensesCommand command,
        CancellationToken cancellationToken)
    {
        var ids = command.Rows.Select(row => row.PaidByMemberId).Distinct().ToList();
        var members = await db.HouseholdMembers
            .AsNoTracking()
            .Where(member => member.HouseholdId == command.HouseholdId && ids.Contains(member.Id))
            .Select(member => new { member.Id, member.IsBudgetParticipant })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return members
            .Where(member => member.IsBudgetParticipant)
            .ToDictionary(member => member.Id, _ => true);
    }

    private async Task<HashSet<string>> ExistingFingerprintsAsync(
        ImportBudgetExpensesCommand command,
        CancellationToken cancellationToken)
    {
        var from = command.Rows.Min(row => row.Date);
        var to = command.Rows.Max(row => row.Date);
        var existing = await db.BudgetExpenses
            .AsNoTracking()
            .Where(expense => expense.HouseholdId == command.HouseholdId
                && expense.Date >= from
                && expense.Date <= to)
            .Select(expense => new
            {
                expense.Date,
                expense.AmountMinor,
                expense.CategoryId,
                expense.SubcategoryId,
                expense.PaidByMemberId,
                expense.Description,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return existing
            .Select(expense => Fingerprint(
                expense.Date,
                expense.AmountMinor,
                expense.CategoryId,
                expense.SubcategoryId,
                expense.PaidByMemberId,
                expense.Description))
            .ToHashSet(StringComparer.Ordinal);
    }

    private static (List<BudgetExpense> Expenses, int Skipped) BuildExpenses(
        ImportBudgetExpensesCommand command,
        Dictionary<string, BudgetCategory> categories,
        Dictionary<string, BudgetSubcategory> subcategories,
        Dictionary<Guid, bool> participants,
        HashSet<string> existing,
        DateTimeOffset now)
    {
        var expenses = new List<BudgetExpense>(command.Rows.Count);
        var skipped = 0;

        foreach (var row in command.Rows)
        {
            RequireRow(row);

            if (!categories.TryGetValue(NormalizeKey(row.Category), out var category))
            {
                throw new ApplicationValidationException(
                    $"Line {row.Line.ToString(CultureInfo.InvariantCulture)}: the category '{row.Category}' has no mapping.");
            }

            var subcategory = FindSubcategory(row, subcategories);
            if (!participants.ContainsKey(row.PaidByMemberId))
            {
                throw new ApplicationValidationException(
                    $"Line {row.Line.ToString(CultureInfo.InvariantCulture)}: the payer does not take part in this household's budget.");
            }

            var description = CreateBudgetExpenseHandler.NullIfWhiteSpace(row.Description);
            var fingerprint = Fingerprint(row.Date, row.AmountMinor, category.Id, subcategory?.Id, row.PaidByMemberId, description);

            // Re-importing the same file is harmless: only rows that were not already stored are
            // added. Fingerprints are built from the rows that existed before this import, so
            // genuine duplicates inside one file are still imported.
            if (existing.Contains(fingerprint))
            {
                skipped++;
                continue;
            }

            expenses.Add(new BudgetExpense
            {
                HouseholdId = command.HouseholdId,
                Date = row.Date,
                AmountMinor = row.AmountMinor,
                CategoryId = category.Id,
                SubcategoryId = subcategory?.Id,
                PaidByMemberId = row.PaidByMemberId,
                Description = description,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
            });
        }

        return (expenses, skipped);
    }

    private static BudgetSubcategory? FindSubcategory(
        ImportBudgetExpenseRow row,
        Dictionary<string, BudgetSubcategory> subcategories)
    {
        if (string.IsNullOrWhiteSpace(row.Subcategory))
        {
            return null;
        }

        return subcategories.TryGetValue(SubcategoryKey(row.Category, row.Subcategory), out var subcategory)
            ? subcategory
            : throw new ApplicationValidationException(
                $"Line {row.Line.ToString(CultureInfo.InvariantCulture)}: the subcategory '{row.Subcategory}' of '{row.Category}' has no mapping.");
    }

    private static void RequireRow(ImportBudgetExpenseRow row)
    {
        if (string.IsNullOrWhiteSpace(row.Category))
        {
            throw new ApplicationValidationException(
                $"Line {row.Line.ToString(CultureInfo.InvariantCulture)}: the category is required.");
        }

        BudgetGuards.RequirePositive(row.AmountMinor, "expense");
        BudgetGuards.RequireYear(row.Date.Year);

        if (row.Description is { Length: > 500 })
        {
            throw new ApplicationValidationException(
                $"Line {row.Line.ToString(CultureInfo.InvariantCulture)}: the description must be 500 characters or fewer.");
        }
    }

    private static string Fingerprint(
        DateOnly date,
        long amountMinor,
        Guid categoryId,
        Guid? subcategoryId,
        Guid paidByMemberId,
        string? description)
    {
        string[] parts =
        [
            date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            amountMinor.ToString(CultureInfo.InvariantCulture),
            categoryId.ToString(),
            subcategoryId?.ToString() ?? string.Empty,
            paidByMemberId.ToString(),
            description?.Trim() ?? string.Empty,
        ];

        return string.Join('|', parts);
    }

    private static string RequireSource(string? value, string what)
    {
        var source = NormalizeKey(value);
        if (source.Length == 0)
        {
            throw new ApplicationValidationException($"A {what} mapping has an empty name.");
        }

        return source;
    }

    private static string NormalizeKey(string? value) => value?.Trim() ?? string.Empty;

    private static string SubcategoryKey(string category, string subcategory) =>
        string.Concat(NormalizeKey(category), "\u001f", NormalizeKey(subcategory));

    private static string SubcategoryNameKey(Guid categoryId, string subcategory) =>
        string.Concat(categoryId.ToString(), "\u001f", NormalizeKey(subcategory));
}

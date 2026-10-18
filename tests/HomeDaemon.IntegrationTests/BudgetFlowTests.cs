using HomeDaemon.Application.Budget;
using HomeDaemon.Application.Common;
using HomeDaemon.Application.Households;
using HomeDaemon.Domain.Households;
using HomeDaemon.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;

namespace HomeDaemon.IntegrationTests;

public sealed class BudgetFlowTests
{
    private static readonly DateOnly Year2025 = new(2025, 1, 1);

    [Fact]
    public async Task Expense_IsSplitByIncomeProportion_AndIncomeChangeIsRetroactive()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var db = database.Context;
        var time = TestTimeProvider.At(TestSeed.DefaultDate);
        var household = await TestSeed.HouseholdAsync(db, time);
        var alice = await TestSeed.MemberAsync(db, time, household.Id, "Alice", isBudgetParticipant: true);
        var bob = await TestSeed.MemberAsync(db, time, household.Id, "Bob", isBudgetParticipant: true);
        var category = await TestSeed.BudgetCategoryAsync(db, time, household.Id);

        await TestSeed.BudgetIncomeAsync(db, time, household.Id, alice.Id, 2025, 6, 6000);
        await TestSeed.BudgetIncomeAsync(db, time, household.Id, bob.Id, 2025, 6, 4000);
        await TestSeed.BudgetExpenseAsync(db, time, household.Id, new DateOnly(2025, 6, 15), 10000, category.Id, alice.Id);

        var handler = new GetBudgetSummaryHandler(db, time);
        var first = await handler.HandleAsync(new GetBudgetSummaryQuery(household.Id, 2025), TestContext.Current.CancellationToken);

        first.ProportionAvailable.Should().BeTrue();
        Alice(first).Share.Should().Be(0.6m);
        Alice(first).BalanceMinor.Should().Be(4000);
        Bob(first).BalanceMinor.Should().Be(-4000);

        // Changing Bob's income re-splits the expense that was already recorded.
        await TestSeed.BudgetIncomeAsync(db, time, household.Id, bob.Id, 2025, 6, 6000);

        var second = await handler.HandleAsync(new GetBudgetSummaryQuery(household.Id, 2025), TestContext.Current.CancellationToken);

        Alice(second).Share.Should().Be(0.5m);
        Alice(second).BalanceMinor.Should().Be(5000);
        Bob(second).BalanceMinor.Should().Be(-5000);
    }

    [Fact]
    public async Task Transfer_SettlesOnlyItsSettlementYear()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var db = database.Context;
        var time = TestTimeProvider.At(TestSeed.DefaultDate);
        var household = await TestSeed.HouseholdAsync(db, time);
        var alice = await TestSeed.MemberAsync(db, time, household.Id, "Alice", isBudgetParticipant: true);
        var bob = await TestSeed.MemberAsync(db, time, household.Id, "Bob", isBudgetParticipant: true);
        var category = await TestSeed.BudgetCategoryAsync(db, time, household.Id);

        await TestSeed.BudgetIncomeAsync(db, time, household.Id, alice.Id, 2025, 1, 6000);
        await TestSeed.BudgetIncomeAsync(db, time, household.Id, bob.Id, 2025, 1, 4000);
        await TestSeed.BudgetExpenseAsync(db, time, household.Id, new DateOnly(2025, 1, 10), 10000, category.Id, alice.Id);

        var transfers = new CreateBudgetTransferHandler(db, time);

        // Bob repays Alice the 4000 he owes for 2025.
        await transfers.HandleAsync(
            new CreateBudgetTransferCommand(household.Id, new DateOnly(2025, 2, 1), 2025, 4000, bob.Id, alice.Id, null),
            TestContext.Current.CancellationToken);

        var handler = new GetBudgetSummaryHandler(db, time);
        var settled = await handler.HandleAsync(new GetBudgetSummaryQuery(household.Id, 2025), TestContext.Current.CancellationToken);

        Alice(settled).BalanceMinor.Should().Be(0);
        Bob(settled).BalanceMinor.Should().Be(0);

        // A transfer settling 2026 must not change the 2025 balance.
        await transfers.HandleAsync(
            new CreateBudgetTransferCommand(household.Id, new DateOnly(2026, 1, 5), 2026, 1000, bob.Id, alice.Id, null),
            TestContext.Current.CancellationToken);

        var stillSettled = await handler.HandleAsync(new GetBudgetSummaryQuery(household.Id, 2025), TestContext.Current.CancellationToken);
        Alice(stillSettled).BalanceMinor.Should().Be(0);
    }

    [Fact]
    public async Task Subcategory_MustBelongToTheSelectedCategory()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var db = database.Context;
        var time = TestTimeProvider.At(TestSeed.DefaultDate);
        var household = await TestSeed.HouseholdAsync(db, time);
        var alice = await TestSeed.MemberAsync(db, time, household.Id, "Alice", isBudgetParticipant: true);
        var food = await TestSeed.BudgetCategoryAsync(db, time, household.Id, "Food");
        var home = await TestSeed.BudgetCategoryAsync(db, time, household.Id, "Home");
        var groceries = await TestSeed.BudgetSubcategoryAsync(db, time, household.Id, food.Id);

        var handler = new CreateBudgetExpenseHandler(db, time);
        var request = () => handler.HandleAsync(
            new CreateBudgetExpenseCommand(
                household.Id,
                Year2025,
                1000,
                home.Id,
                groceries.Id,
                alice.Id,
                null),
            TestContext.Current.CancellationToken);

        await request.Should().ThrowAsync<ApplicationValidationException>();
    }

    [Fact]
    public async Task Category_DeletionIsBlockedWhileExpensesUseIt()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var db = database.Context;
        var time = TestTimeProvider.At(TestSeed.DefaultDate);
        var household = await TestSeed.HouseholdAsync(db, time);
        var alice = await TestSeed.MemberAsync(db, time, household.Id, "Alice", isBudgetParticipant: true);
        var category = await TestSeed.BudgetCategoryAsync(db, time, household.Id, "Food");
        await TestSeed.BudgetExpenseAsync(db, time, household.Id, Year2025, 1000, category.Id, alice.Id);

        var handler = new DeleteBudgetCategoryHandler(db);
        var request = () => handler.HandleAsync(
            new DeleteBudgetCategoryCommand(household.Id, category.Id),
            TestContext.Current.CancellationToken);

        await request.Should().ThrowAsync<EntityConflictException>();
    }

    [Fact]
    public async Task Expenses_PageThroughEveryRowWithoutGapsOrDuplicates()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var db = database.Context;
        var time = TestTimeProvider.At(TestSeed.DefaultDate);
        var household = await TestSeed.HouseholdAsync(db, time);
        var alice = await TestSeed.MemberAsync(db, time, household.Id, "Alice", isBudgetParticipant: true);
        var category = await TestSeed.BudgetCategoryAsync(db, time, household.Id);

        for (var day = 1; day <= 5; day++)
        {
            await TestSeed.BudgetExpenseAsync(db, time, household.Id, new DateOnly(2025, 3, day), 1000 + day, category.Id, alice.Id);
        }

        var handler = new GetBudgetExpensesHandler(db);
        var collected = new List<BudgetExpenseDto>();
        string? cursor = null;

        do
        {
            var page = await handler.HandleAsync(
                new GetBudgetExpensesQuery(household.Id, 2025, null, null, null, null, null, cursor, 2),
                TestContext.Current.CancellationToken);

            collected.AddRange(page.Items);
            cursor = page.NextCursor;
        }
        while (cursor is not null);

        collected.Should().HaveCount(5);
        collected.Select(item => item.Id).Should().OnlyHaveUniqueItems();
        collected.Select(item => item.Date).Should().BeInDescendingOrder();
    }

    [Fact]
    public async Task Income_UpsertReplacesTheSameMonthInsteadOfDuplicatingIt()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var db = database.Context;
        var time = TestTimeProvider.At(TestSeed.DefaultDate);
        var household = await TestSeed.HouseholdAsync(db, time);
        var alice = await TestSeed.MemberAsync(db, time, household.Id, "Alice", isBudgetParticipant: true);

        await TestSeed.BudgetIncomeAsync(db, time, household.Id, alice.Id, 2025, 3, 1000);
        await TestSeed.BudgetIncomeAsync(db, time, household.Id, alice.Id, 2025, 3, 2500);

        db.BudgetIncomes.Should().ContainSingle(income => income.MemberId == alice.Id && income.Year == 2025 && income.Month == 3)
            .Which.AmountMinor.Should().Be(2500);
    }

    [Fact]
    public async Task Summary_FlagsMissingIncomeMonthsForTheWholePastYear()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var db = database.Context;
        var time = TestTimeProvider.At(TestSeed.DefaultDate);
        var household = await TestSeed.HouseholdAsync(db, time);
        var alice = await TestSeed.MemberAsync(db, time, household.Id, "Alice", isBudgetParticipant: true);
        var bob = await TestSeed.MemberAsync(db, time, household.Id, "Bob", isBudgetParticipant: true);

        await TestSeed.BudgetIncomeAsync(db, time, household.Id, alice.Id, 2025, 1, 1000);
        await TestSeed.BudgetIncomeAsync(db, time, household.Id, alice.Id, 2025, 2, 1000);
        await TestSeed.BudgetIncomeAsync(db, time, household.Id, bob.Id, 2025, 1, 1000);

        var handler = new GetBudgetSummaryHandler(db, time);
        var summary = await handler.HandleAsync(new GetBudgetSummaryQuery(household.Id, 2025), TestContext.Current.CancellationToken);

        summary.IncomeComplete.Should().BeFalse();
        Alice(summary).MissingMonths.Should().HaveCount(10);
        Bob(summary).MissingMonths.Should().HaveCount(11);
    }

    [Fact]
    public async Task Summary_WithoutIncomeLeavesTheProportionUnavailable()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var db = database.Context;
        var time = TestTimeProvider.At(TestSeed.DefaultDate);
        var household = await TestSeed.HouseholdAsync(db, time);
        var alice = await TestSeed.MemberAsync(db, time, household.Id, "Alice", isBudgetParticipant: true);
        var bob = await TestSeed.MemberAsync(db, time, household.Id, "Bob", isBudgetParticipant: true);
        var category = await TestSeed.BudgetCategoryAsync(db, time, household.Id);
        await TestSeed.BudgetExpenseAsync(db, time, household.Id, Year2025, 1000, category.Id, alice.Id);

        var handler = new GetBudgetSummaryHandler(db, time);
        var summary = await handler.HandleAsync(new GetBudgetSummaryQuery(household.Id, 2025), TestContext.Current.CancellationToken);

        summary.ProportionAvailable.Should().BeFalse();
        Alice(summary).DueMinor.Should().BeNull();
        Bob(summary).DueMinor.Should().BeNull();
        Alice(summary).PaidMinor.Should().Be(1000);
    }

    [Fact]
    public async Task Subcategory_DeletionIsBlockedWhileExpensesUseIt()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var db = database.Context;
        var time = TestTimeProvider.At(TestSeed.DefaultDate);
        var household = await TestSeed.HouseholdAsync(db, time);
        var alice = await TestSeed.MemberAsync(db, time, household.Id, "Alice", isBudgetParticipant: true);
        var category = await TestSeed.BudgetCategoryAsync(db, time, household.Id);
        var subcategory = await TestSeed.BudgetSubcategoryAsync(db, time, household.Id, category.Id);
        await TestSeed.BudgetExpenseAsync(db, time, household.Id, Year2025, 1000, category.Id, alice.Id, subcategory.Id);

        var handler = new DeleteBudgetSubcategoryHandler(db);
        var request = () => handler.HandleAsync(
            new DeleteBudgetSubcategoryCommand(household.Id, subcategory.Id),
            TestContext.Current.CancellationToken);

        await request.Should().ThrowAsync<EntityConflictException>();
    }

    [Fact]
    public async Task Transfer_RejectsTheSamePersonOnBothSides()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var db = database.Context;
        var time = TestTimeProvider.At(TestSeed.DefaultDate);
        var household = await TestSeed.HouseholdAsync(db, time);
        var alice = await TestSeed.MemberAsync(db, time, household.Id, "Alice", isBudgetParticipant: true);

        var handler = new CreateBudgetTransferHandler(db, time);
        var request = () => handler.HandleAsync(
            new CreateBudgetTransferCommand(household.Id, Year2025, 2025, 1000, alice.Id, alice.Id, null),
            TestContext.Current.CancellationToken);

        await request.Should().ThrowAsync<ApplicationValidationException>();
    }

    [Fact]
    public async Task Expense_RejectsAPayerWhoDoesNotTakePartInTheBudget()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var db = database.Context;
        var time = TestTimeProvider.At(TestSeed.DefaultDate);
        var household = await TestSeed.HouseholdAsync(db, time);
        var alice = await TestSeed.MemberAsync(db, time, household.Id, "Alice", isBudgetParticipant: true);
        var guest = await TestSeed.MemberAsync(db, time, household.Id, "Guest");
        var category = await TestSeed.BudgetCategoryAsync(db, time, household.Id);

        var handler = new CreateBudgetExpenseHandler(db, time);
        var request = () => handler.HandleAsync(
            new CreateBudgetExpenseCommand(household.Id, Year2025, 1000, category.Id, null, guest.Id, null),
            TestContext.Current.CancellationToken);

        await request.Should().ThrowAsync<ApplicationValidationException>();
    }

    [Fact]
    public async Task Expense_RejectsACategoryFromAnotherHousehold()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var db = database.Context;
        var time = TestTimeProvider.At(TestSeed.DefaultDate);
        var household = await TestSeed.HouseholdAsync(db, time);
        var alice = await TestSeed.MemberAsync(db, time, household.Id, "Alice", isBudgetParticipant: true);

        var otherHousehold = new Household
        {
            Name = "Other",
            TimeZoneId = "UTC",
            Currency = "EUR",
            CreatedAtUtc = time.GetUtcNow(),
            UpdatedAtUtc = time.GetUtcNow(),
        };
        db.Households.Add(otherHousehold);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var foreignCategory = await TestSeed.BudgetCategoryAsync(db, time, otherHousehold.Id, "Foreign");

        var handler = new CreateBudgetExpenseHandler(db, time);
        var request = () => handler.HandleAsync(
            new CreateBudgetExpenseCommand(household.Id, Year2025, 1000, foreignCategory.Id, null, alice.Id, null),
            TestContext.Current.CancellationToken);

        await request.Should().ThrowAsync<EntityNotFoundException>();
    }

    [Fact]
    public async Task Expense_UpdateAndDeleteRecomputeTheBalance()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var db = database.Context;
        var time = TestTimeProvider.At(TestSeed.DefaultDate);
        var household = await TestSeed.HouseholdAsync(db, time);
        var alice = await TestSeed.MemberAsync(db, time, household.Id, "Alice", isBudgetParticipant: true);
        var bob = await TestSeed.MemberAsync(db, time, household.Id, "Bob", isBudgetParticipant: true);
        var category = await TestSeed.BudgetCategoryAsync(db, time, household.Id);

        await TestSeed.BudgetIncomeAsync(db, time, household.Id, alice.Id, 2025, 1, 6000);
        await TestSeed.BudgetIncomeAsync(db, time, household.Id, bob.Id, 2025, 1, 4000);
        var expense = await TestSeed.BudgetExpenseAsync(db, time, household.Id, new DateOnly(2025, 1, 10), 10000, category.Id, alice.Id);

        var summary = new GetBudgetSummaryHandler(db, time);

        await new UpdateBudgetExpenseHandler(db, time).HandleAsync(
            new UpdateBudgetExpenseCommand(household.Id, expense.Id, new DateOnly(2025, 1, 10), 20000, category.Id, null, alice.Id, null),
            TestContext.Current.CancellationToken);

        var updated = await summary.HandleAsync(new GetBudgetSummaryQuery(household.Id, 2025), TestContext.Current.CancellationToken);
        updated.TotalExpensesMinor.Should().Be(20000);
        Alice(updated).BalanceMinor.Should().Be(8000);

        await new DeleteBudgetExpenseHandler(db).HandleAsync(
            new DeleteBudgetExpenseCommand(household.Id, expense.Id),
            TestContext.Current.CancellationToken);

        var deleted = await summary.HandleAsync(new GetBudgetSummaryQuery(household.Id, 2025), TestContext.Current.CancellationToken);
        deleted.TotalExpensesMinor.Should().Be(0);
        Alice(deleted).BalanceMinor.Should().Be(0);
    }

    [Fact]
    public async Task Expense_UnknownIdReturnsNotFound()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var db = database.Context;
        var time = TestTimeProvider.At(TestSeed.DefaultDate);
        var household = await TestSeed.HouseholdAsync(db, time);

        var handler = new GetBudgetExpenseHandler(db);
        var request = () => handler.HandleAsync(
            new GetBudgetExpenseQuery(household.Id, Guid.NewGuid()),
            TestContext.Current.CancellationToken);

        await request.Should().ThrowAsync<EntityNotFoundException>();
    }

    [Fact]
    public async Task Income_CanBeCleared()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var db = database.Context;
        var time = TestTimeProvider.At(TestSeed.DefaultDate);
        var household = await TestSeed.HouseholdAsync(db, time);
        var alice = await TestSeed.MemberAsync(db, time, household.Id, "Alice", isBudgetParticipant: true);
        var income = await TestSeed.BudgetIncomeAsync(db, time, household.Id, alice.Id, 2025, 3, 1000);

        await new DeleteBudgetIncomeHandler(db).HandleAsync(
            new DeleteBudgetIncomeCommand(household.Id, income.IncomeId!.Value),
            TestContext.Current.CancellationToken);

        (await db.BudgetIncomes.CountAsync(TestContext.Current.CancellationToken)).Should().Be(0);
    }

    [Fact]
    public async Task Summary_ScopesExpensesAndIncomesToTheirOwnYear()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var db = database.Context;
        var time = TestTimeProvider.At(TestSeed.DefaultDate);
        var household = await TestSeed.HouseholdAsync(db, time);
        var alice = await TestSeed.MemberAsync(db, time, household.Id, "Alice", isBudgetParticipant: true);
        var bob = await TestSeed.MemberAsync(db, time, household.Id, "Bob", isBudgetParticipant: true);
        var category = await TestSeed.BudgetCategoryAsync(db, time, household.Id);

        await TestSeed.BudgetIncomeAsync(db, time, household.Id, alice.Id, 2025, 1, 6000);
        await TestSeed.BudgetIncomeAsync(db, time, household.Id, bob.Id, 2025, 1, 4000);
        await TestSeed.BudgetIncomeAsync(db, time, household.Id, alice.Id, 2026, 1, 5000);
        await TestSeed.BudgetIncomeAsync(db, time, household.Id, bob.Id, 2026, 1, 5000);

        await TestSeed.BudgetExpenseAsync(db, time, household.Id, new DateOnly(2025, 6, 1), 10000, category.Id, alice.Id);
        await TestSeed.BudgetExpenseAsync(db, time, household.Id, new DateOnly(2026, 6, 1), 3000, category.Id, bob.Id);

        var handler = new GetBudgetSummaryHandler(db, time);

        var year2025 = await handler.HandleAsync(new GetBudgetSummaryQuery(household.Id, 2025), TestContext.Current.CancellationToken);
        year2025.TotalExpensesMinor.Should().Be(10000);
        Alice(year2025).BalanceMinor.Should().Be(4000);

        var year2026 = await handler.HandleAsync(new GetBudgetSummaryQuery(household.Id, 2026), TestContext.Current.CancellationToken);
        year2026.TotalExpensesMinor.Should().Be(3000);
        Alice(year2026).BalanceMinor.Should().Be(-1500);
        Bob(year2026).BalanceMinor.Should().Be(1500);
    }

    [Fact]
    public async Task Category_And_Subcategory_CanBeRenamed()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var db = database.Context;
        var time = TestTimeProvider.At(TestSeed.DefaultDate);
        var household = await TestSeed.HouseholdAsync(db, time);
        var category = await TestSeed.BudgetCategoryAsync(db, time, household.Id, "Food");

        var renamed = await new UpdateBudgetCategoryHandler(db, time).HandleAsync(
            new UpdateBudgetCategoryCommand(household.Id, category.Id, "Home", 5, false),
            TestContext.Current.CancellationToken);

        renamed.Name.Should().Be("Home");
        renamed.SortOrder.Should().Be(5);

        var subcategory = await TestSeed.BudgetSubcategoryAsync(db, time, household.Id, category.Id, "Groceries");
        var renamedSub = await new UpdateBudgetSubcategoryHandler(db, time).HandleAsync(
            new UpdateBudgetSubcategoryCommand(household.Id, subcategory.Id, "Bills", 2, false),
            TestContext.Current.CancellationToken);

        renamedSub.Name.Should().Be("Bills");
        renamedSub.SortOrder.Should().Be(2);
    }

    [Fact]
    public async Task Expenses_CanBeFilteredByCategoryAndPayer()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var db = database.Context;
        var time = TestTimeProvider.At(TestSeed.DefaultDate);
        var household = await TestSeed.HouseholdAsync(db, time);
        var alice = await TestSeed.MemberAsync(db, time, household.Id, "Alice", isBudgetParticipant: true);
        var bob = await TestSeed.MemberAsync(db, time, household.Id, "Bob", isBudgetParticipant: true);
        var food = await TestSeed.BudgetCategoryAsync(db, time, household.Id, "Food");
        var home = await TestSeed.BudgetCategoryAsync(db, time, household.Id, "Home");

        await TestSeed.BudgetExpenseAsync(db, time, household.Id, new DateOnly(2025, 2, 1), 1000, food.Id, alice.Id);
        await TestSeed.BudgetExpenseAsync(db, time, household.Id, new DateOnly(2025, 2, 2), 2000, home.Id, bob.Id);

        var handler = new GetBudgetExpensesHandler(db);

        var byCategory = await handler.HandleAsync(
            new GetBudgetExpensesQuery(household.Id, 2025, null, null, food.Id, null, null, null, null),
            TestContext.Current.CancellationToken);
        byCategory.Items.Should().ContainSingle().Which.AmountMinor.Should().Be(1000);

        var byPayer = await handler.HandleAsync(
            new GetBudgetExpensesQuery(household.Id, 2025, null, null, null, null, bob.Id, null, null),
            TestContext.Current.CancellationToken);
        byPayer.Items.Should().ContainSingle().Which.PaidByMemberName.Should().Be("Bob");
    }

    [Fact]
    public async Task Expenses_KeepPagingStableWhenNewerRowsAreInserted()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var db = database.Context;
        var time = TestTimeProvider.At(TestSeed.DefaultDate);
        var household = await TestSeed.HouseholdAsync(db, time);
        var alice = await TestSeed.MemberAsync(db, time, household.Id, "Alice", isBudgetParticipant: true);
        var category = await TestSeed.BudgetCategoryAsync(db, time, household.Id);

        for (var day = 1; day <= 5; day++)
        {
            await TestSeed.BudgetExpenseAsync(db, time, household.Id, new DateOnly(2025, 3, day), 1000 + day, category.Id, alice.Id);
        }

        var handler = new GetBudgetExpensesHandler(db);
        var first = await handler.HandleAsync(
            new GetBudgetExpensesQuery(household.Id, 2025, null, null, null, null, null, null, 2),
            TestContext.Current.CancellationToken);

        first.Items.Should().HaveCount(2);

        // A row newer than everything already paged must not shift or duplicate the following pages.
        await TestSeed.BudgetExpenseAsync(db, time, household.Id, new DateOnly(2025, 3, 10), 9999, category.Id, alice.Id);

        var collected = new List<BudgetExpenseDto>(first.Items);
        var cursor = first.NextCursor;

        while (cursor is not null)
        {
            var page = await handler.HandleAsync(
                new GetBudgetExpensesQuery(household.Id, 2025, null, null, null, null, null, cursor, 2),
                TestContext.Current.CancellationToken);

            collected.AddRange(page.Items);
            cursor = page.NextCursor;
        }

        collected.Should().HaveCount(5);
        collected.Select(item => item.Id).Should().OnlyHaveUniqueItems();
        collected.Select(item => item.Date).Should().BeInDescendingOrder();
    }

    private static BudgetMemberSummaryDto Alice(BudgetSummaryDto summary) =>
        summary.Members.Single(member => member.Name == "Alice");

    private static BudgetMemberSummaryDto Bob(BudgetSummaryDto summary) =>
        summary.Members.Single(member => member.Name == "Bob");
}

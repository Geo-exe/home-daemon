using HomeDaemon.Domain.Budget;

namespace HomeDaemon.UnitTests.Budget;

public sealed class BudgetIncomeStatusTests
{
    [Fact]
    public void ReportsTheMonthsThatWereNotEntered()
    {
        var missing = BudgetIncomeStatus.FindMissingMonths([1, 2, 3], 1, 6);

        Assert.Equal([4, 5, 6], missing);
    }

    [Fact]
    public void ReportsNothingWhenEveryExpectedMonthIsPresent()
    {
        var missing = BudgetIncomeStatus.FindMissingMonths([1, 2, 3, 4, 5, 6], 1, 6);

        missing.Should().BeEmpty();
    }

    [Fact]
    public void RespectsTheFirstExpectedMonthOfAPersonAddedMidYear()
    {
        var missing = BudgetIncomeStatus.FindMissingMonths([1, 2, 3, 4, 5, 6], 3, 6);

        missing.Should().BeEmpty();
    }

    [Fact]
    public void ReportsNothingForAPersonAddedAfterTheWindow()
    {
        var missing = BudgetIncomeStatus.FindMissingMonths([], 7, 6);

        missing.Should().BeEmpty();
    }

    [Fact]
    public void ReportsNothingForAFutureYear()
    {
        var missing = BudgetIncomeStatus.FindMissingMonths([], 1, 0);

        missing.Should().BeEmpty();
    }

    [Fact]
    public void ClampsOutOfRangeMonths()
    {
        var missing = BudgetIncomeStatus.FindMissingMonths([], 0, 13);

        Assert.Equal(Enumerable.Range(1, 12), missing);
    }
}

using HomeDaemon.Domain.Budget;

namespace HomeDaemon.UnitTests.Budget;

public sealed class BudgetBalanceCalculatorTests
{
    private static readonly Guid Alice = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid Bob = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static readonly Guid Cara = Guid.Parse("00000000-0000-0000-0000-000000000003");

    [Fact]
    public void SplitsExpensesInProportionToAnnualIncome()
    {
        var result = Calculate(
            [Alice, Bob],
            new Dictionary<Guid, long> { [Alice] = 6000, [Bob] = 4000 },
            [new BudgetExpenseLine(Alice, 10000)],
            []);

        result.ProportionAvailable.Should().BeTrue();
        result.TotalIncomeMinor.Should().Be(10000);
        result.TotalExpensesMinor.Should().Be(10000);

        var alice = Member(result, Alice);
        alice.Share.Should().Be(0.6m);
        alice.PaidMinor.Should().Be(10000);
        alice.DueMinor.Should().Be(6000);
        alice.BalanceMinor.Should().Be(4000);

        var bob = Member(result, Bob);
        bob.Share.Should().Be(0.4m);
        bob.DueMinor.Should().Be(4000);
        bob.BalanceMinor.Should().Be(-4000);
    }

    [Fact]
    public void RecomputedProportionIsRetroactiveAcrossTheWholeYear()
    {
        var before = Calculate(
            [Alice, Bob],
            new Dictionary<Guid, long> { [Alice] = 6000, [Bob] = 4000 },
            [new BudgetExpenseLine(Alice, 10000)],
            []);

        Member(before, Alice).BalanceMinor.Should().Be(4000);

        // A single income change re-splits the expense already recorded earlier in the year.
        var after = Calculate(
            [Alice, Bob],
            new Dictionary<Guid, long> { [Alice] = 6000, [Bob] = 6000 },
            [new BudgetExpenseLine(Alice, 10000)],
            []);

        Member(after, Alice).Share.Should().Be(0.5m);
        Member(after, Alice).BalanceMinor.Should().Be(5000);
        Member(after, Bob).BalanceMinor.Should().Be(-5000);
    }

    [Fact]
    public void RoundingKeepsTheDueAmountsEqualToTheTotal()
    {
        var result = Calculate(
            [Alice, Bob, Cara],
            new Dictionary<Guid, long> { [Alice] = 2000, [Bob] = 1000, [Cara] = 1000 },
            [new BudgetExpenseLine(Alice, 4001)],
            []);

        Member(result, Alice).DueMinor.Should().Be(2001);
        Member(result, Bob).DueMinor.Should().Be(1000);
        Member(result, Cara).DueMinor.Should().Be(1000);

        result.Members.Sum(member => member.DueMinor!.Value).Should().Be(4001);
    }

    [Fact]
    public void EqualRemaindersAreBrokenDeterministically()
    {
        var result = Calculate(
            [Alice, Bob, Cara],
            new Dictionary<Guid, long> { [Alice] = 1000, [Bob] = 1000, [Cara] = 1000 },
            [new BudgetExpenseLine(Alice, 100)],
            []);

        Member(result, Alice).DueMinor.Should().Be(34);
        Member(result, Bob).DueMinor.Should().Be(33);
        Member(result, Cara).DueMinor.Should().Be(33);
    }

    [Fact]
    public void WithoutIncomeTheProportionIsUnavailable()
    {
        var result = Calculate(
            [Alice, Bob],
            new Dictionary<Guid, long>(),
            [new BudgetExpenseLine(Alice, 10000)],
            []);

        result.ProportionAvailable.Should().BeFalse();
        result.Members.Should().OnlyContain(member => member.Share == null && member.DueMinor == null && member.BalanceMinor == null);

        var alice = Member(result, Alice);
        alice.PaidMinor.Should().Be(10000);
        alice.AnnualIncomeMinor.Should().Be(0);
    }

    [Fact]
    public void TransferFromTheDebtorSettlesTheBalance()
    {
        var result = Calculate(
            [Alice, Bob],
            new Dictionary<Guid, long> { [Alice] = 6000, [Bob] = 4000 },
            [new BudgetExpenseLine(Alice, 10000)],
            [new BudgetTransferLine(Bob, Alice, 4000)]);

        Member(result, Alice).BalanceMinor.Should().Be(0);
        Member(result, Bob).BalanceMinor.Should().Be(0);
    }

    [Fact]
    public void BalancesAlwaysSumToZero()
    {
        var result = Calculate(
            [Alice, Bob, Cara],
            new Dictionary<Guid, long> { [Alice] = 3000, [Bob] = 5000, [Cara] = 2000 },
            [new BudgetExpenseLine(Alice, 7000), new BudgetExpenseLine(Bob, 3333)],
            [new BudgetTransferLine(Cara, Bob, 1000)]);

        result.Members.Sum(member => member.BalanceMinor!.Value).Should().Be(0);
    }

    private static BudgetBalance Calculate(
        IReadOnlyList<Guid> memberIds,
        IReadOnlyDictionary<Guid, long> annualIncomeMinor,
        IReadOnlyList<BudgetExpenseLine> expenses,
        IReadOnlyList<BudgetTransferLine> transfers) =>
        BudgetBalanceCalculator.Calculate(memberIds, annualIncomeMinor, expenses, transfers);

    private static BudgetMemberBalance Member(BudgetBalance balance, Guid memberId) =>
        balance.Members.Single(member => member.MemberId == memberId);
}

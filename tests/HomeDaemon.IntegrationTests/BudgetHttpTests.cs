using System.Net;
using System.Net.Http.Json;
using HomeDaemon.IntegrationTests.Support;

namespace HomeDaemon.IntegrationTests;

/// <summary>
/// End-to-end coverage of the budget HTTP surface: the shared authentication and antiforgery
/// pipeline, request validation and the full split-and-settle flow through real serialization.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class BudgetHttpTests
{
    private readonly HomeDaemonApiFactory _factory;

    public BudgetHttpTests(HomeDaemonApiFactory factory) => _factory = factory;

    [Fact]
    public async Task BudgetEndpointsRequireAuthentication()
    {
        using var client = _factory.CreateClient();

        var summary = await client.GetAsync("/api/v1/budget/summary?year=2026", TestContext.Current.CancellationToken);
        summary.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var categories = await client.GetAsync("/api/v1/budget/categories", TestContext.Current.CancellationToken);
        categories.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task BudgetWritesRequireTheAntiforgeryToken()
    {
        using var admin = await _factory.SignInAsAdminAsync();

        var withoutToken = await HomeDaemonApiFactory.PostWithoutTokenAsync(
            admin,
            "/api/v1/budget/categories",
            new { name = "No token budget", sortOrder = 0 });
        withoutToken.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var withToken = await HomeDaemonApiFactory.PostAsync(
            admin,
            "/api/v1/budget/categories",
            new { name = "With token budget", sortOrder = 0 });
        withToken.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task SummaryRejectsAYearOutOfRange()
    {
        using var admin = await _factory.SignInAsAdminAsync();

        var response = await admin.GetAsync("/api/v1/budget/summary?year=1900", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task BudgetFlowSplitsExpensesAndSettlesTransfersOverHttp()
    {
        using var admin = await _factory.SignInAsAdminAsync();

        var alice = await CreateParticipantAsync(admin, "Budget Alice");
        var bob = await CreateParticipantAsync(admin, "Budget Bob");
        var category = await CreateCategoryAsync(admin, "Budget Food");

        await PutIncomeAsync(admin, alice.Id, 2026, 6, 6000);
        await PutIncomeAsync(admin, bob.Id, 2026, 6, 4000);

        var expenseResponse = await HomeDaemonApiFactory.PostAsync(admin, "/api/v1/budget/expenses", new
        {
            date = new DateOnly(2026, 6, 15),
            amountMinor = 10000,
            categoryId = category.Id,
            subcategoryId = (Guid?)null,
            paidByMemberId = alice.Id,
            description = (string?)null,
        });
        expenseResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var expense = (await expenseResponse.Content.ReadFromJsonAsync<BudgetExpenseResponse>())!;
        expense.Date.Should().Be(new DateOnly(2026, 6, 15));
        expense.AmountMinor.Should().Be(10000);
        expense.PaidByMemberName.Should().Be("Budget Alice");

        var split = await admin.GetFromJsonAsync<BudgetSummaryResponse>("/api/v1/budget/summary?year=2026", TestContext.Current.CancellationToken);
        split!.ProportionAvailable.Should().BeTrue();
        Member(split, "Budget Alice").BalanceMinor.Should().Be(4000);
        Member(split, "Budget Bob").BalanceMinor.Should().Be(-4000);

        var transferResponse = await HomeDaemonApiFactory.PostAsync(admin, "/api/v1/budget/transfers", new
        {
            date = new DateOnly(2026, 7, 1),
            settlementYear = 2026,
            amountMinor = 4000,
            fromMemberId = bob.Id,
            toMemberId = alice.Id,
            description = (string?)null,
        });
        transferResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var settled = await admin.GetFromJsonAsync<BudgetSummaryResponse>("/api/v1/budget/summary?year=2026", TestContext.Current.CancellationToken);
        Member(settled!, "Budget Alice").BalanceMinor.Should().Be(0);
        Member(settled!, "Budget Bob").BalanceMinor.Should().Be(0);
    }

    private static BudgetMemberSummaryResponse Member(BudgetSummaryResponse summary, string name) =>
        summary.Members.Single(member => member.Name == name);

    private static async Task<MemberDto> CreateParticipantAsync(HttpClient admin, string name)
    {
        var response = await HomeDaemonApiFactory.PostAsync(admin, "/api/v1/household/members", new
        {
            name,
            color = (string?)null,
            isAssignable = true,
            isBudgetParticipant = true,
            todoListEntityId = (string?)null,
            personEntityId = (string?)null,
            calendarEntityIds = Array.Empty<string>(),
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<MemberDto>())!;
    }

    private static async Task<BudgetCategoryResponse> CreateCategoryAsync(HttpClient admin, string name)
    {
        var response = await HomeDaemonApiFactory.PostAsync(admin, "/api/v1/budget/categories", new { name, sortOrder = 0 });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<BudgetCategoryResponse>())!;
    }

    private static async Task PutIncomeAsync(HttpClient admin, Guid memberId, int year, int month, long amountMinor)
    {
        var response = await HomeDaemonApiFactory.PutAsync(admin, "/api/v1/budget/incomes", new { memberId, year, month, amountMinor });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}

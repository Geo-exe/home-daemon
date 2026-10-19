using System.Net;
using System.Net.Http.Json;
using HomeDaemon.IntegrationTests.Support;

namespace HomeDaemon.IntegrationTests;

/// <summary>
/// End-to-end coverage of the administrator-only CSV import: explicit mapping, creation of the
/// missing categories, idempotent re-import and all-or-nothing validation.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class BudgetImportTests
{
    private readonly HomeDaemonApiFactory _factory;

    public BudgetImportTests(HomeDaemonApiFactory factory) => _factory = factory;

    [Fact]
    public async Task ImportCreatesTheMissingCategoriesAndIsSafeToRepeat()
    {
        using var admin = await _factory.SignInAsAdminAsync();

        var alice = await CreateParticipantAsync(admin, "Import Alice");
        var bob = await CreateParticipantAsync(admin, "Import Bob");
        var food = await CreateCategoryAsync(admin, "Import Food");
        var groceries = await CreateSubcategoryAsync(admin, food.Id, "Groceries");

        var request = new
        {
            rows = new object[]
            {
                new
                {
                    line = 2,
                    date = new DateOnly(2042, 2, 7),
                    amountMinor = 9619,
                    category = "Import Food",
                    subcategory = "Groceries",
                    paidByMemberId = alice.Id,
                    description = "Carrefour",
                },
                new
                {
                    line = 3,
                    date = new DateOnly(2042, 2, 9),
                    amountMinor = 55000,
                    category = "Import Travel",
                    subcategory = "Trains",
                    paidByMemberId = bob.Id,
                    description = (string?)null,
                },
            },
            categories = new object[]
            {
                new { sourceName = "Import Food", categoryId = (Guid?)food.Id, create = false },
                new { sourceName = "Import Travel", categoryId = (Guid?)null, create = true },
            },
            subcategories = new object[]
            {
                new
                {
                    categorySourceName = "Import Food",
                    sourceName = "Groceries",
                    subcategoryId = (Guid?)groceries.Id,
                    create = false,
                },
                new
                {
                    categorySourceName = "Import Travel",
                    sourceName = "Trains",
                    subcategoryId = (Guid?)null,
                    create = true,
                },
            },
        };

        var first = await HomeDaemonApiFactory.PostAsync(admin, "/api/v1/budget/imports", request);
        var firstBody = await first.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        first.StatusCode.Should().Be(HttpStatusCode.OK, firstBody);
        var firstResult = await first.Content.ReadFromJsonAsync<BudgetImportResponse>(TestContext.Current.CancellationToken);
        firstResult.Should().Be(new BudgetImportResponse(2, 0));

        var categories = await admin.GetFromJsonAsync<List<BudgetCategoryDetail>>(
            "/api/v1/budget/categories",
            TestContext.Current.CancellationToken);
        var travel = categories!.Single(category => category.Name == "Import Travel");
        travel.Subcategories.Should().ContainSingle(subcategory => subcategory.Name == "Trains");

        // The same file imported again updates nothing and reports the rows as skipped.
        var second = await HomeDaemonApiFactory.PostAsync(admin, "/api/v1/budget/imports", request);
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        var secondResult = await second.Content.ReadFromJsonAsync<BudgetImportResponse>(TestContext.Current.CancellationToken);
        secondResult.Should().Be(new BudgetImportResponse(0, 2));
    }

    [Fact]
    public async Task AInvalidRowLeavesTheWholeImportUntouched()
    {
        using var admin = await _factory.SignInAsAdminAsync();
        var alice = await CreateParticipantAsync(admin, "Atomic Alice");

        var request = new
        {
            rows = new object[]
            {
                new
                {
                    line = 2,
                    date = new DateOnly(2042, 3, 1),
                    amountMinor = 1000,
                    category = "Atomic Category",
                    subcategory = (string?)null,
                    paidByMemberId = alice.Id,
                    description = "Valid row",
                },
                new
                {
                    line = 3,
                    date = new DateOnly(2042, 3, 2),
                    amountMinor = 0,
                    category = "Atomic Category",
                    subcategory = (string?)null,
                    paidByMemberId = alice.Id,
                    description = "Invalid row",
                },
            },
            categories = new object[]
            {
                new { sourceName = "Atomic Category", categoryId = (Guid?)null, create = true },
            },
            subcategories = Array.Empty<object>(),
        };

        var response = await HomeDaemonApiFactory.PostAsync(admin, "/api/v1/budget/imports", request);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var categories = await admin.GetFromJsonAsync<List<BudgetCategoryDetail>>(
            "/api/v1/budget/categories",
            TestContext.Current.CancellationToken);
        categories!.Should().NotContain(category => category.Name == "Atomic Category");
    }

    [Fact]
    public async Task OnlyAdministratorsCanImport()
    {
        using var admin = await _factory.SignInAsAdminAsync();
        var created = await CreateUserAsync(admin, "import-standard@home-daemon.test");

        using var user = await _factory.SignInAsync(created.User.Email, created.TemporaryPassword);
        var changed = await HomeDaemonApiFactory.PostAsync(user, "/api/v1/auth/change-password", new
        {
            currentPassword = created.TemporaryPassword,
            newPassword = "A-Settled-Password1",
        });
        changed.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var response = await HomeDaemonApiFactory.PostAsync(user, "/api/v1/budget/imports", new
        {
            rows = Array.Empty<object>(),
            categories = Array.Empty<object>(),
            subcategories = Array.Empty<object>(),
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

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

    private static async Task<BudgetCategoryDetail> CreateCategoryAsync(HttpClient admin, string name)
    {
        var response = await HomeDaemonApiFactory.PostAsync(admin, "/api/v1/budget/categories", new { name, sortOrder = 0 });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<BudgetCategoryDetail>())!;
    }

    private static async Task<BudgetSubcategoryResponse> CreateSubcategoryAsync(HttpClient admin, Guid categoryId, string name)
    {
        var response = await HomeDaemonApiFactory.PostAsync(
            admin,
            $"/api/v1/budget/categories/{categoryId}/subcategories",
            new { name, sortOrder = 0 });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<BudgetSubcategoryResponse>())!;
    }

    private static async Task<TemporaryPasswordDto> CreateUserAsync(HttpClient admin, string email)
    {
        var response = await HomeDaemonApiFactory.PostAsync(admin, "/api/v1/admin/users", new
        {
            email,
            displayName = (string?)null,
            householdMemberId = (Guid?)null,
            isAdmin = false,
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<TemporaryPasswordDto>())!;
    }
}

internal sealed record BudgetImportResponse(int Imported, int Skipped);

internal sealed record BudgetCategoryDetail(
    Guid Id,
    string Name,
    int SortOrder,
    IReadOnlyList<BudgetSubcategoryResponse> Subcategories);

internal sealed record BudgetSubcategoryResponse(Guid Id, Guid CategoryId, string Name, int SortOrder);

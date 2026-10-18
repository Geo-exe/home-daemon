using System.Net;
using System.Net.Http.Json;
using HomeDaemon.Application.Households;
using HomeDaemon.IntegrationTests.Support;

namespace HomeDaemon.IntegrationTests;

/// <summary>
/// Covers the first-run setup path: the household is created by an administrator, the currency
/// is optional, and a standard account cannot create it.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class HouseholdOnboardingTests
{
    private readonly HomeDaemonApiFactory _factory;

    public HouseholdOnboardingTests(HomeDaemonApiFactory factory) => _factory = factory;

    [Fact]
    public async Task CreatingAHouseholdDefaultsToEurAndAcceptsAChosenCurrency()
    {
        var time = TestTimeProvider.At(TestSeed.DefaultDate);

        await using var first = await SqliteTestDatabase.CreateAsync();
        var defaulted = await new CreateHouseholdHandler(first.Context, time)
            .HandleAsync(new CreateHouseholdCommand("Home", "UTC"), TestContext.Current.CancellationToken);

        defaulted.Currency.Should().Be("EUR");

        await using var second = await SqliteTestDatabase.CreateAsync();
        var chosen = await new CreateHouseholdHandler(second.Context, time)
            .HandleAsync(new CreateHouseholdCommand("Casa", "Europe/Rome", "gbp"), TestContext.Current.CancellationToken);

        chosen.Currency.Should().Be("GBP");
    }

    [Fact]
    public async Task CreatingAHouseholdIsAnAdministrativeAction()
    {
        using var admin = await _factory.SignInAsAdminAsync();

        var created = await CreateStandardUserAsync(admin);
        using var user = await SignInAndSettlePasswordAsync(created);

        var forbidden = await HomeDaemonApiFactory.PostAsync(user, "/api/v1/households", new { name = "Theirs", timeZoneId = "UTC" });
        forbidden.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // The administrator is allowed through; the fixture already has a household, so the
        // handler answers with a validation failure rather than an authorization one.
        var allowed = await HomeDaemonApiFactory.PostAsync(admin, "/api/v1/households", new { name = "Another", timeZoneId = "UTC" });
        allowed.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private static async Task<TemporaryPasswordDto> CreateStandardUserAsync(HttpClient admin)
    {
        var response = await HomeDaemonApiFactory.PostAsync(admin, "/api/v1/admin/users", new
        {
            email = $"member-{Guid.NewGuid():N}@home-daemon.test",
            displayName = (string?)null,
            householdMemberId = (Guid?)null,
            isAdmin = false,
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<TemporaryPasswordDto>())!;
    }

    private async Task<HttpClient> SignInAndSettlePasswordAsync(TemporaryPasswordDto created)
    {
        var client = await _factory.SignInAsync(created.User.Email, created.TemporaryPassword);

        var changed = await HomeDaemonApiFactory.PostAsync(client, "/api/v1/auth/change-password", new
        {
            currentPassword = created.TemporaryPassword,
            newPassword = "A-Settled-Password1",
        });
        changed.StatusCode.Should().Be(HttpStatusCode.NoContent);
        return client;
    }
}

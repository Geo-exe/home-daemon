using System.Net;
using System.Net.Http.Json;
using HomeDaemon.Application.Common;
using HomeDaemon.IntegrationTests.Support;
using Microsoft.AspNetCore.Mvc.Testing;

namespace HomeDaemon.IntegrationTests;

/// <summary>
/// End-to-end coverage of the authentication pipeline: identity cookies, antiforgery, the
/// mandatory password change, role-based administration and completion attribution.
/// </summary>
public sealed class AuthFlowTests : IClassFixture<HomeDaemonApiFactory>
{
    private readonly HomeDaemonApiFactory _factory;

    public AuthFlowTests(HomeDaemonApiFactory factory) => _factory = factory;

    [Fact]
    public async Task AnonymousRequestsAreRejectedAndTheApiKeyStillWorks()
    {
        using var client = _factory.CreateClient();

        var anonymous = await client.GetAsync("/api/v1/chores", TestContext.Current.CancellationToken);
        anonymous.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/chores");
        request.Headers.TryAddWithoutValidation("X-Api-Key", HomeDaemonApiFactory.ApiKey);
        var authorized = await client.SendAsync(request, TestContext.Current.CancellationToken);
        authorized.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task NewAccountMustChangeItsPasswordBeforeUsingTheApi()
    {
        using var admin = await _factory.SignInAsAdminAsync();
        var created = await CreateUserAsync(admin, "must-change@home-daemon.test", isAdmin: false);

        using var user = await _factory.SignInAsync(created.User.Email, created.TemporaryPassword);

        var me = await user.GetFromJsonAsync<CurrentUserDto>("/api/v1/auth/me", TestContext.Current.CancellationToken);
        me!.MustChangePassword.Should().BeTrue();

        var blocked = await user.GetAsync("/api/v1/chores", TestContext.Current.CancellationToken);
        blocked.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var changed = await HomeDaemonApiFactory.PostAsync(user, "/api/v1/auth/change-password", new
        {
            currentPassword = created.TemporaryPassword,
            newPassword = "A-Brand-New-Password1",
        });
        changed.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var allowed = await user.GetAsync("/api/v1/chores", TestContext.Current.CancellationToken);
        allowed.StatusCode.Should().Be(HttpStatusCode.OK);

        var after = await user.GetFromJsonAsync<CurrentUserDto>("/api/v1/auth/me", TestContext.Current.CancellationToken);
        after!.MustChangePassword.Should().BeFalse();
    }

    [Fact]
    public async Task MutatingRequestsRequireTheAntiforgeryToken()
    {
        using var admin = await _factory.SignInAsAdminAsync();

        var withoutToken = await HomeDaemonApiFactory.PostWithoutTokenAsync(admin, "/api/v1/household/rooms", new { name = "No token", sortOrder = 0 });
        withoutToken.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var withToken = await HomeDaemonApiFactory.PostAsync(admin, "/api/v1/household/rooms", new { name = "With token", sortOrder = 0 });
        withToken.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task StandardUserCannotReachAdministration()
    {
        using var admin = await _factory.SignInAsAdminAsync();
        var created = await CreateUserAsync(admin, "standard-user@home-daemon.test", isAdmin: false);

        using var user = await SignInAndSettlePasswordAsync(created);

        var adminUsers = await user.GetAsync("/api/v1/admin/users", TestContext.Current.CancellationToken);
        adminUsers.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var chores = await user.GetAsync("/api/v1/chores", TestContext.Current.CancellationToken);
        chores.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task DeactivatedAccountCanNoLongerSignIn()
    {
        using var admin = await _factory.SignInAsAdminAsync();
        var created = await CreateUserAsync(admin, "deactivated@home-daemon.test", isAdmin: false);

        var deactivate = await HomeDaemonApiFactory.PostAsync(admin, $"/api/v1/admin/users/{created.User.Id}/deactivate", null);
        deactivate.StatusCode.Should().Be(HttpStatusCode.OK);

        using var client = _factory.CreateClient();
        var login = await HomeDaemonApiFactory.PostAsync(client, "/api/v1/auth/login", new { email = created.User.Email, password = created.TemporaryPassword });
        login.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AdministratorCannotDeactivateItself()
    {
        using var admin = await _factory.SignInAsAdminAsync();

        var me = await admin.GetFromJsonAsync<CurrentUserDto>("/api/v1/auth/me", TestContext.Current.CancellationToken);
        var deactivate = await HomeDaemonApiFactory.PostAsync(admin, $"/api/v1/admin/users/{me!.Id}/deactivate", null);

        deactivate.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task SignOutEndsTheSession()
    {
        using var admin = await _factory.SignInAsAdminAsync();

        var before = await admin.GetAsync("/api/v1/auth/me", TestContext.Current.CancellationToken);
        before.StatusCode.Should().Be(HttpStatusCode.OK);

        var logout = await HomeDaemonApiFactory.PostAsync(admin, "/api/v1/auth/logout", null);
        logout.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var after = await admin.GetAsync("/api/v1/auth/me", TestContext.Current.CancellationToken);
        after.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ManualCompletionIsAttributedToTheSignedInAccountAndItsMember()
    {
        using var admin = await _factory.SignInAsAdminAsync();

        var member = await CreateMemberAsync(admin, "Alice");
        var chore = await CreateChoreAsync(admin, "Clean oven");
        var created = await CreateUserAsync(admin, "completer@home-daemon.test", isAdmin: false, member.Id);

        using var user = await SignInAndSettlePasswordAsync(created);

        var completed = await HomeDaemonApiFactory.PostAsync(user, $"/api/v1/chores/{chore.Id}/complete", new
        {
            completedDate = (DateOnly?)null,
            idempotencyKey = "auth-flow-completion",
        });
        completed.StatusCode.Should().Be(HttpStatusCode.OK);

        var history = await user.GetFromJsonAsync<HistoryPageDto>($"/api/v1/chores/history?choreId={chore.Id}", TestContext.Current.CancellationToken);
        var item = history!.Items.Should().ContainSingle().Subject;

        item.CompletedByUserId.Should().Be(created.User.Id);
        item.CompletedByMemberId.Should().Be(member.Id);
        item.CompletedByMemberName.Should().Be("Alice");
        item.Source.Should().Be("Manual");
    }

    [Fact]
    public async Task AdministratorsCanListAndLinkAccounts()
    {
        using var admin = await _factory.SignInAsAdminAsync();

        var member = await CreateMemberAsync(admin, "Bob");
        var created = await CreateUserAsync(admin, "linked@home-daemon.test", isAdmin: false, member.Id);

        created.User.HouseholdMemberId.Should().Be(member.Id);
        created.User.MustChangePassword.Should().BeTrue();

        var list = await admin.GetFromJsonAsync<CursorPageDto<UserDto>>("/api/v1/admin/users?pageSize=100", TestContext.Current.CancellationToken);
        list!.Items.Should().Contain(user => user.Email == "linked@home-daemon.test");

        // The member is taken, so linking a second account to them must fail.
        var clash = await HomeDaemonApiFactory.PostAsync(admin, "/api/v1/admin/users", new
        {
            email = "clash@home-daemon.test",
            displayName = (string?)null,
            householdMemberId = member.Id,
            isAdmin = false,
        });
        clash.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    private async Task<TemporaryPasswordDto> CreateUserAsync(HttpClient admin, string email, bool isAdmin, Guid? memberId = null)
    {
        var response = await HomeDaemonApiFactory.PostAsync(admin, "/api/v1/admin/users", new
        {
            email,
            displayName = (string?)null,
            householdMemberId = memberId,
            isAdmin,
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<TemporaryPasswordDto>())!;
    }

    private static async Task<MemberDto> CreateMemberAsync(HttpClient admin, string name)
    {
        var response = await HomeDaemonApiFactory.PostAsync(admin, "/api/v1/household/members", new
        {
            name,
            color = (string?)null,
            isAssignable = true,
            todoListEntityId = $"todo.{name.ToLowerInvariant()}",
            personEntityId = (string?)null,
            calendarEntityIds = Array.Empty<string>(),
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<MemberDto>())!;
    }

    private static async Task<ChoreDto> CreateChoreAsync(HttpClient admin, string title)
    {
        var response = await HomeDaemonApiFactory.PostAsync(admin, "/api/v1/chores", new
        {
            title,
            description = (string?)null,
            roomId = (Guid?)null,
            isActive = true,
            estimatedMinutes = 30,
            effortWeight = (double?)null,
            preferSameRoomGrouping = false,
            frequency = "Daily",
            interval = 1,
            mode = "Dynamic",
            anchorDate = (DateOnly?)null,
            assignmentPolicy = "Both",
            fixedAssigneeMemberId = (Guid?)null,
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<ChoreDto>())!;
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

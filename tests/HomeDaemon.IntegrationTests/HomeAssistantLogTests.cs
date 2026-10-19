using System.Net;
using System.Net.Http.Json;
using HomeDaemon.Application.Abstractions.Integrations;
using HomeDaemon.Application.Integrations.HomeAssistant;
using HomeDaemon.DataAccess.Persistence;
using HomeDaemon.Domain.Integrations;
using HomeDaemon.IntegrationTests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace HomeDaemon.IntegrationTests;

/// <summary>
/// End-to-end coverage of the administrator diagnostics: recording, sanitization, filtering,
/// cursor paging and the retention cleanup.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class HomeAssistantLogTests
{
    private readonly HomeDaemonApiFactory _factory;

    public HomeAssistantLogTests(HomeDaemonApiFactory factory) => _factory = factory;

    [Fact]
    public void TheTypedHomeAssistantClientResolvesWithItsRecorder()
    {
        using var scope = _factory.Services.CreateScope();

        scope.ServiceProvider.GetRequiredService<IHomeAssistantClient>().Should().NotBeNull();
    }

    [Fact]
    public async Task TheLogEndpointRequiresTheAdministratorRole()
    {
        using var anonymous = _factory.CreateClient();
        var unknown = await anonymous.GetAsync(
            "/api/v1/admin/home-assistant-logs",
            TestContext.Current.CancellationToken);
        unknown.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        using var admin = await _factory.SignInAsAdminAsync();
        var created = await CreateUserAsync(admin, "ha-logs-standard@home-daemon.test");
        using var user = await _factory.SignInAsync(created.User.Email, created.TemporaryPassword);
        var changed = await HomeDaemonApiFactory.PostAsync(user, "/api/v1/auth/change-password", new
        {
            currentPassword = created.TemporaryPassword,
            newPassword = "A-Settled-Password1",
        });
        changed.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var forbidden = await user.GetAsync(
            "/api/v1/admin/home-assistant-logs",
            TestContext.Current.CancellationToken);
        forbidden.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task RecordedFailuresAreSanitizedAndFilterable()
    {
        using var admin = await _factory.SignInAsAdminAsync();

        using (var scope = _factory.Services.CreateScope())
        {
            var recorder = scope.ServiceProvider.GetRequiredService<IHomeAssistantCallRecorder>();
            recorder.Record(new HomeAssistantCallRecord(
                "test.todo_add",
                "todo.sanitize",
                "POST",
                Succeeded: false,
                StatusCode: 401,
                DurationMs: 15,
                ErrorMessage: "first line\r\nsecond line",
                IsTransient: false));
        }

        var page = await admin.GetFromJsonAsync<HomeAssistantLogPageDto>(
            "/api/v1/admin/home-assistant-logs?status=Failed&operation=test.todo_add&entityId=todo.sanitize",
            TestContext.Current.CancellationToken);

        var item = page!.Items.Should().ContainSingle().Subject;
        item.Status.Should().Be("Failed");
        item.StatusCode.Should().Be(401);
        item.DurationMs.Should().Be(15);
        item.ErrorMessage.Should().Be("first line second line");
        item.IsTransient.Should().BeFalse();
    }

    [Fact]
    public async Task TheLogIsPagedNewestFirst()
    {
        using var admin = await _factory.SignInAsAdminAsync();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HomeDaemonDbContext>();
            var now = DateTimeOffset.UtcNow;
            db.HomeAssistantCallLogs.AddRange(
                Log("test.page", now.AddMinutes(-3)),
                Log("test.page", now.AddMinutes(-2)),
                Log("test.page", now.AddMinutes(-1)));
            db.SaveChanges();
        }

        var first = await admin.GetFromJsonAsync<HomeAssistantLogPageDto>(
            "/api/v1/admin/home-assistant-logs?operation=test.page&pageSize=2",
            TestContext.Current.CancellationToken);

        first!.Items.Should().HaveCount(2);
        first.HasMore.Should().BeTrue();
        first.NextCursor.Should().NotBeNullOrWhiteSpace();
        first.Items[0].OccurredAtUtc.Should().BeAfter(first.Items[1].OccurredAtUtc);

        var second = await admin.GetFromJsonAsync<HomeAssistantLogPageDto>(
            $"/api/v1/admin/home-assistant-logs?operation=test.page&pageSize=2&cursor={Uri.EscapeDataString(first.NextCursor!)}",
            TestContext.Current.CancellationToken);

        second!.Items.Should().ContainSingle();
        second.HasMore.Should().BeFalse();
    }

    [Fact]
    public async Task CleanupKeepsOnlyTheRetentionWindow()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HomeDaemonDbContext>();
            db.HomeAssistantCallLogs.AddRange(
                Log("test.retention", DateTimeOffset.UtcNow.AddDays(-40)),
                Log("test.retention", DateTimeOffset.UtcNow.AddDays(-1)));
            db.SaveChanges();

            var handler = scope.ServiceProvider.GetRequiredService<CleanupHomeAssistantCallLogsHandler>();
            await handler.HandleAsync(TestContext.Current.CancellationToken);
        }

        using var verify = _factory.Services.CreateScope();
        var verifyDb = verify.ServiceProvider.GetRequiredService<HomeDaemonDbContext>();
        var remaining = verifyDb.HomeAssistantCallLogs
            .Where(log => log.Operation == "test.retention")
            .ToList();

        remaining.Should().ContainSingle();
        remaining[0].OccurredAtUtc.Should().BeCloseTo(DateTimeOffset.UtcNow.AddDays(-1), TimeSpan.FromMinutes(5));
    }

    private static HomeAssistantCallLog Log(string operation, DateTimeOffset occurredAtUtc) =>
        new()
        {
            OccurredAtUtc = occurredAtUtc,
            Operation = operation,
            EntityId = "todo.test",
            Method = "POST",
            Status = HomeAssistantCallStatus.Succeeded,
            StatusCode = 200,
            DurationMs = 5,
        };

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

internal sealed record HomeAssistantLogPageDto(
    IReadOnlyList<HomeAssistantLogDto> Items,
    string? NextCursor,
    bool HasMore);

internal sealed record HomeAssistantLogDto(
    Guid Id,
    DateTimeOffset OccurredAtUtc,
    string Operation,
    string? EntityId,
    string Method,
    string Status,
    int? StatusCode,
    int DurationMs,
    string? ErrorMessage,
    bool? IsTransient);

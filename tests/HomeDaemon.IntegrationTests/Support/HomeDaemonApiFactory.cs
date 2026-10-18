using System.Net.Http.Json;
using HomeDaemon.Api.Auth;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HomeDaemon.IntegrationTests.Support;

/// <summary>
/// Hosts the real API with its own throwaway SQLite file, so the authentication pipeline
/// (cookies, antiforgery, account state) is exercised end to end.
/// </summary>
public sealed class HomeDaemonApiFactory : WebApplicationFactory<Program>
{
    public const string AdminEmail = "admin@home-daemon.test";
    public const string AdminPassword = "Changed-Admin-Password1";
    public const string ApiKey = "integration-test-api-key";

    private const string BootstrapPassword = "Bootstrap-Admin-Password1";
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"home-daemon-test-{Guid.NewGuid():N}.db");
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _initialised;

    public HomeDaemonApiFactory()
    {
        // Minimal hosting loads appsettings.json after the factory's own configuration
        // callbacks, so environment variables are the reliable way to point the host at a
        // throwaway database and to set the machine credential.
        Environment.SetEnvironmentVariable("ConnectionStrings__HomeDaemon", $"Data Source={_databasePath}");
        Environment.SetEnvironmentVariable("Database__ApplyMigrationsOnStartup", "true");
        Environment.SetEnvironmentVariable("Backup__Enabled", "false");
        Environment.SetEnvironmentVariable("ApiKey__Value", ApiKey);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.UseEnvironment("Development");
    }

    /// <summary>
    /// Bootstraps the administrator, then clears the mandatory password change so the other
    /// tests can sign in with <see cref="AdminPassword"/>. Runs at most once.
    /// </summary>
    public async Task EnsureSeededAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_initialised)
            {
                return;
            }

            await AdminBootstrapper.CreateFirstAdminAsync(Services, AdminEmail, BootstrapPassword).ConfigureAwait(false);

            using var client = CreateClient();
            var login = await PostAsync(client, "/api/v1/auth/login", new { email = AdminEmail, password = BootstrapPassword }).ConfigureAwait(false);
            login.EnsureSuccessStatusCode();

            var changed = await PostAsync(client, "/api/v1/auth/change-password", new { currentPassword = BootstrapPassword, newPassword = AdminPassword }).ConfigureAwait(false);
            changed.EnsureSuccessStatusCode();

            // Most authorization assertions are clearer against an endpoint that needs a
            // household, so the fixture configures one.
            var household = await PostAsync(client, "/api/v1/households", new { name = "Test Home", timeZoneId = "UTC" }).ConfigureAwait(false);
            household.EnsureSuccessStatusCode();

            _ = _initialised = true;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Creates a fresh client, signs in and returns it; the caller owns disposal.</summary>
    public async Task<HttpClient> SignInAsync(string email, string password)
    {
        var client = CreateClient();
        var response = await PostAsync(client, "/api/v1/auth/login", new { email, password }).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return client;
    }

    /// <summary>Signs in with the administrator account used by the fixtures.</summary>
    public async Task<HttpClient> SignInAsAdminAsync()
    {
        await EnsureSeededAsync().ConfigureAwait(false);
        return await SignInAsync(AdminEmail, AdminPassword).ConfigureAwait(false);
    }

    /// <summary>
    /// Sends a request with the antiforgery header, the way the browser frontend will. Fetching
    /// a fresh token first keeps the request token and its cookie in step.
    /// </summary>
    public static async Task<HttpResponseMessage> PostAsync(HttpClient client, string path, object? body)
    {
        ArgumentNullException.ThrowIfNull(client);

        var token = await GetCsrfTokenAsync(client).ConfigureAwait(false);
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = body is null ? null : JsonContent.Create(body),
        };
        request.Headers.TryAddWithoutValidation("X-CSRF-TOKEN", token);
        return await client.SendAsync(request).ConfigureAwait(false);
    }

    /// <summary>Sends a mutating request without the antiforgery header, to prove it is enforced.</summary>
    public static Task<HttpResponseMessage> PostWithoutTokenAsync(HttpClient client, string path, object? body)
    {
        ArgumentNullException.ThrowIfNull(client);
        return client.PostAsJsonAsync(path, body);
    }

    public static async Task<string> GetCsrfTokenAsync(HttpClient client)
    {
        ArgumentNullException.ThrowIfNull(client);

        var payload = await client.GetFromJsonAsync<CsrfTokenDto>("/api/v1/auth/csrf").ConfigureAwait(false);
        return payload?.Token ?? string.Empty;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing && File.Exists(_databasePath))
        {
            File.Delete(_databasePath);
        }
    }
}

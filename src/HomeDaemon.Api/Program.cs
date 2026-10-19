using System.Text;
using System.Text.Json.Serialization;
using HomeDaemon.Api;
using HomeDaemon.Api.Auth;
using HomeDaemon.Api.Endpoints;
using HomeDaemon.Api.Workers;
using HomeDaemon.Application;
using HomeDaemon.Application.Sync;
using HomeDaemon.DataAccess;
using HomeDaemon.DataAccess.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

const string createAdminCommand = "create-admin";

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddApplication(builder.Configuration);
builder.Services.AddDataAccess(builder.Configuration);

builder.Services.Configure<ApiKeyOptions>(builder.Configuration.GetSection("ApiKey"));
builder.Services.Configure<RecalculationScheduleOptions>(builder.Configuration.GetSection("Recalculation"));
builder.Services.Configure<BackupScheduleOptions>(builder.Configuration.GetSection("Backup"));
builder.Services.Configure<TodoSyncOptions>(builder.Configuration.GetSection("TodoSync"));

// Opt-in support for running behind a TLS-terminating reverse proxy, so the app restores the
// original https scheme instead of seeing the plain http hop the proxy uses internally.
builder.Services.AddForwardedHeaders(builder.Configuration);

builder.Services.AddIdentityCore<ApplicationUser>(options =>
{
    options.User.RequireUniqueEmail = true;
    options.Password.RequiredLength = 10;
    options.Password.RequireNonAlphanumeric = false;
})
.AddRoles<IdentityRole<Guid>>()
.AddEntityFrameworkStores<HomeDaemonDbContext>()
.AddSignInManager()
.AddDefaultTokenProviders();

// Home Assistant calls carry the machine API key; browser sessions use the Identity cookie.
// There is deliberately no bearer token: nothing sensitive is handed to JavaScript.
builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultScheme = ApiKeyDefaults.PolicyScheme;
        options.DefaultAuthenticateScheme = ApiKeyDefaults.PolicyScheme;
        options.DefaultChallengeScheme = IdentityConstants.ApplicationScheme;
    })
    .AddPolicyScheme(ApiKeyDefaults.PolicyScheme, "Home Assistant API key or identity cookie", options =>
    {
        options.ForwardDefaultSelector = context =>
            context.Request.Headers.ContainsKey(ApiKeyOptions.HeaderName)
                ? ApiKeyDefaults.Scheme
                : IdentityConstants.ApplicationScheme;
    })
    .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(ApiKeyDefaults.Scheme, static _ => { })
    .AddIdentityCookies();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "homedaaemon.session";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
    options.SlidingExpiration = true;
    options.ExpireTimeSpan = TimeSpan.FromDays(14);

    // API clients expect status codes, not a redirect to a login page.
    options.Events.OnRedirectToLogin = context =>
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    };
    options.Events.OnRedirectToAccessDenied = context =>
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    };
});

builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.Name = "homedaaemon.csrf";
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
});

builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
    .AddPolicy(AccountRoles.AdminPolicy, policy => policy.RequireRole(AccountRoles.Admin));

builder.Services.AddHealthChecks().AddDbContextCheck<HomeDaemonDbContext>("sqlite");

builder.Services.AddHostedService<ScheduleRunWorker>();
builder.Services.AddHostedService<TodoSyncWorker>();
builder.Services.AddHostedService<NightlySchedulingWorker>();
builder.Services.AddHostedService<DatabaseBackupWorker>();
builder.Services.AddHostedService<HomeAssistantLogCleanupWorker>();

var app = builder.Build();

if (Array.IndexOf(args, createAdminCommand) >= 0)
{
    await CreateAdminAsync(app).ConfigureAwait(false);
    return;
}

// Must run before anything that depends on the scheme or the client address, most importantly
// authentication and the cookie policy.
app.UseForwardedHeaders();

app.UseExceptionHandler();
app.UseDefaultFiles();
app.UseStaticFiles();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
}

app.UseRouting();
app.UseAuthentication();
app.UseMiddleware<AccountStatusMiddleware>();
app.UseMiddleware<CsrfValidationMiddleware>();
app.UseAuthorization();

app.MapHealthEndpoints();
app.MapAuthEndpoints();
app.MapAdminUserEndpoints();
app.MapAdminHomeAssistantLogEndpoints();

app.MapHouseholdEndpoints();
app.MapChoreEndpoints();
app.MapPlannerEndpoints();
app.MapSchedulingEndpoints();
app.MapBudgetEndpoints();
app.MapHomeAssistantEndpoints();

// Single-page app fallback. Only paths that are not the API or a probe get the app shell, so an
// unknown API route still answers 404 instead of a page. It is anonymous on purpose: the login
// screen has to load before there is any session.
app.MapFallback(ServeWebAppAsync).AllowAnonymous();

if (builder.Configuration.GetValue("Database:ApplyMigrationsOnStartup", false))
{
    await ApplyMigrationsAsync(app).ConfigureAwait(false);
}

await app.RunAsync().ConfigureAwait(false);

static async Task ApplyMigrationsAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<HomeDaemonDbContext>();
    await db.Database.MigrateAsync().ConfigureAwait(false);
}

static async Task ServeWebAppAsync(HttpContext context)
{
    ArgumentNullException.ThrowIfNull(context);

    var path = context.Request.Path;
    if (path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase)
        || path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }

    var webRoot = context.RequestServices.GetRequiredService<IWebHostEnvironment>().WebRootPath;
    var indexFile = webRoot is null ? null : Path.Combine(webRoot, "index.html");

    if (indexFile is null || !File.Exists(indexFile))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        context.Response.ContentType = "text/plain; charset=utf-8";
        await context.Response
            .WriteAsync("The web app has not been built. Run 'npm run build' in src/HomeDaemon.Web.")
            .ConfigureAwait(false);

        return;
    }

    context.Response.ContentType = "text/html; charset=utf-8";
    await context.Response.SendFileAsync(indexFile).ConfigureAwait(false);
}

static async Task CreateAdminAsync(WebApplication app)
{
    var email = Environment.GetEnvironmentVariable(AdminBootstrapper.EmailVariable);
    if (string.IsNullOrWhiteSpace(email))
    {
        await Console.Error.WriteLineAsync($"Set {AdminBootstrapper.EmailVariable} to the administrator email.").ConfigureAwait(false);
        Environment.ExitCode = 78;
        return;
    }

    var password = Environment.GetEnvironmentVariable(AdminBootstrapper.PasswordVariable);
    if (string.IsNullOrWhiteSpace(password))
    {
        password = ReadPassword();
    }

    if (string.IsNullOrWhiteSpace(password))
    {
        await Console.Error.WriteLineAsync($"Set {AdminBootstrapper.PasswordVariable} or type the password at the prompt.").ConfigureAwait(false);
        Environment.ExitCode = 78;
        return;
    }

    await AdminBootstrapper.MigrateAsync(app.Services, CancellationToken.None).ConfigureAwait(false);
    var result = await AdminBootstrapper.CreateFirstAdminAsync(app.Services, email, password).ConfigureAwait(false);

    await Console.Out.WriteLineAsync(result.Created
        ? $"Created administrator '{result.Email}'. It must change its password at the first sign-in."
        : "An administrator already exists; nothing was created.").ConfigureAwait(false);
}

static string ReadPassword()
{
    if (Console.IsInputRedirected)
    {
        return Console.ReadLine() ?? string.Empty;
    }

    Console.Write("Administrator password: ");
    var password = new StringBuilder();
    while (true)
    {
        var key = Console.ReadKey(intercept: true);
        if (key.Key == ConsoleKey.Enter)
        {
            Console.WriteLine();
            break;
        }

        if (key.Key == ConsoleKey.Backspace)
        {
            if (password.Length > 0)
            {
                password.Length--;
            }

            continue;
        }

        if (!char.IsControl(key.KeyChar))
        {
            password.Append(key.KeyChar);
        }
    }

    return password.ToString();
}

/// <summary>Exposed so the integration tests can host the real application.</summary>
public partial class Program;

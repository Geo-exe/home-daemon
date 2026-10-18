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
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

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

builder.Services.AddIdentityCore<ApplicationUser>(options =>
{
    options.User.RequireUniqueEmail = true;
    options.Password.RequiredLength = 10;
    options.Password.RequireNonAlphanumeric = false;
})
.AddRoles<IdentityRole<Guid>>()
.AddEntityFrameworkStores<HomeDaemonDbContext>()
.AddApiEndpoints()
.AddDefaultTokenProviders();

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultScheme = ApiKeyDefaults.PolicyScheme;
        options.DefaultAuthenticateScheme = ApiKeyDefaults.PolicyScheme;
        options.DefaultChallengeScheme = ApiKeyDefaults.PolicyScheme;
    })
    .AddPolicyScheme(ApiKeyDefaults.PolicyScheme, "API key or bearer token", options =>
    {
        options.ForwardDefaultSelector = context =>
            context.Request.Headers.ContainsKey(ApiKeyOptions.HeaderName)
                ? ApiKeyDefaults.Scheme
                : IdentityConstants.BearerScheme;
    })
    .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(ApiKeyDefaults.Scheme, static _ => { })
    .AddBearerToken(IdentityConstants.BearerScheme);

builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

builder.Services.AddHealthChecks().AddDbContextCheck<HomeDaemonDbContext>("sqlite");

builder.Services.AddHostedService<ScheduleRunWorker>();
builder.Services.AddHostedService<TodoSyncWorker>();
builder.Services.AddHostedService<NightlySchedulingWorker>();
builder.Services.AddHostedService<DatabaseBackupWorker>();

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
}

app.MapHealthEndpoints();
app.MapGroup("/api/v1/auth").WithTags("Auth").MapIdentityApi<ApplicationUser>().AllowAnonymous();

app.MapHouseholdEndpoints();
app.MapChoreEndpoints();
app.MapPlannerEndpoints();
app.MapSchedulingEndpoints();
app.MapHomeAssistantEndpoints();

if (builder.Configuration.GetValue("Database:ApplyMigrationsOnStartup", false))
{
    await ApplyMigrationsAsync(app).ConfigureAwait(false);
}

await app.RunAsync().ConfigureAwait(false);

static async Task ApplyMigrationsAsync(WebApplication app)
{
    using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<HomeDaemonDbContext>();
    await db.Database.MigrateAsync().ConfigureAwait(false);
}

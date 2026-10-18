using System.Net.Http.Headers;
using HomeDaemon.Application.Abstractions;
using HomeDaemon.Application.Abstractions.Integrations;
using HomeDaemon.DataAccess.Backup;
using HomeDaemon.DataAccess.Integrations.HomeAssistant;
using HomeDaemon.DataAccess.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace HomeDaemon.DataAccess;

public static class DataAccessServiceCollectionExtensions
{
    public static IServiceCollection AddDataAccess(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = configuration.GetConnectionString("HomeDaemon") ?? "Data Source=home-daemon.db";

        services.AddSingleton<SqliteConnectionInterceptor>();
        services.AddDbContext<HomeDaemonDbContext>((provider, options) =>
        {
            options.UseSqlite(connectionString);
            options.AddInterceptors(provider.GetRequiredService<SqliteConnectionInterceptor>());
        });

        services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<HomeDaemonDbContext>());
        services.AddScoped<IDatabaseBackupService, SqliteBackupService>();

        services.Configure<HomeAssistantOptions>(configuration.GetSection("HomeAssistant"));
        services.Configure<BackupOptions>(configuration.GetSection("Backup"));

        services.AddHttpClient<IHomeAssistantClient, HttpHomeAssistantClient>((provider, client) =>
        {
            var options = provider.GetRequiredService<IOptions<HomeAssistantOptions>>().Value;
            if (options.BaseUrl is not null)
            {
                client.BaseAddress = options.BaseUrl;
            }

            client.Timeout = TimeSpan.FromSeconds(Math.Max(1, options.TimeoutSeconds));

            if (!string.IsNullOrWhiteSpace(options.Token))
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", options.Token);
            }
        });

        return services;
    }
}

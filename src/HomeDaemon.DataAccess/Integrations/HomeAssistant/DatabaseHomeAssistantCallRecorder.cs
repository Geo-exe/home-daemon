using HomeDaemon.Application.Abstractions.Integrations;
using HomeDaemon.DataAccess.Persistence;
using HomeDaemon.Domain.Integrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace HomeDaemon.DataAccess.Integrations.HomeAssistant;

/// <summary>
/// Writes one diagnostic row per Home Assistant call. It runs in its own scope so recording
/// never touches the caller's change tracker, and it swallows its own failures: a diagnostic
/// that cannot be stored must not break the call it describes.
/// </summary>
public sealed class DatabaseHomeAssistantCallRecorder(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<DatabaseHomeAssistantCallRecorder> logger) : IHomeAssistantCallRecorder
{
    private const int MaxErrorLength = 500;

    public void Record(HomeAssistantCallRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<HomeDaemonDbContext>();
            db.HomeAssistantCallLogs.Add(new HomeAssistantCallLog
            {
                OccurredAtUtc = timeProvider.GetUtcNow(),
                Operation = record.Operation,
                EntityId = record.EntityId,
                Method = record.Method,
                Status = record.Succeeded ? HomeAssistantCallStatus.Succeeded : HomeAssistantCallStatus.Failed,
                StatusCode = record.StatusCode,
                DurationMs = (int)Math.Min(record.DurationMs, int.MaxValue),
                ErrorMessage = Sanitize(record.ErrorMessage),
                IsTransient = record.IsTransient,
            });
            db.SaveChanges();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            HomeAssistantCallLogLog.PersistFailed(logger, exception);
        }
    }

    private static string? Sanitize(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return null;
        }

        var flattened = message.ReplaceLineEndings(" ").Trim();
        return flattened.Length <= MaxErrorLength ? flattened : flattened[..MaxErrorLength];
    }
}

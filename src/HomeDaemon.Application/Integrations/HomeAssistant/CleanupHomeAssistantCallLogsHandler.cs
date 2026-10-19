using HomeDaemon.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace HomeDaemon.Application.Integrations.HomeAssistant;

/// <summary>
/// Deletes diagnostics older than the retention window so the diagnostic table cannot grow
/// without bound.
/// </summary>
public sealed class CleanupHomeAssistantCallLogsHandler(IApplicationDbContext db, TimeProvider timeProvider)
{
    public const int RetentionDays = 30;

    public async Task<int> HandleAsync(CancellationToken cancellationToken)
    {
        var cutoff = timeProvider.GetUtcNow().AddDays(-RetentionDays);
        return await db.HomeAssistantCallLogs
            .Where(log => log.OccurredAtUtc < cutoff)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}

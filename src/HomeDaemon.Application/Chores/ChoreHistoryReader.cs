using HomeDaemon.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace HomeDaemon.Application.Chores;

internal static class ChoreHistoryReader
{
    public static async Task<IReadOnlyList<ChoreCompletionDto>> RecentAsync(
        IApplicationDbContext db,
        Guid choreId,
        CancellationToken cancellationToken,
        int take = 20)
    {
        var history = await db.CompletionHistory
            .AsNoTracking()
            .Where(item => item.ChoreDefinitionId == choreId)
            .OrderByDescending(item => item.CompletedDate)
            .ThenByDescending(item => item.RecordedAtUtc)
            .Take(take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return history.ConvertAll(ChoreMappings.ToDto);
    }
}

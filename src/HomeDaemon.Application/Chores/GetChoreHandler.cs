using HomeDaemon.Application.Abstractions;
using HomeDaemon.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace HomeDaemon.Application.Chores;

public sealed class GetChoreHandler(IApplicationDbContext db)
{
    public async Task<ChoreDetailDto> HandleAsync(GetChoreQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var chore = await db.Chores
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == query.ChoreId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new EntityNotFoundException($"Chore '{query.ChoreId}' was not found.");

        var completions = await ChoreHistoryReader.RecentAsync(db, chore.Id, cancellationToken).ConfigureAwait(false);
        return ChoreMappings.ToDetail(chore, completions);
    }
}

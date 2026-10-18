using HomeDaemon.Application.Abstractions;
using HomeDaemon.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace HomeDaemon.Application.Scheduling;

public sealed class GetScheduleRunHandler(IApplicationDbContext db)
{
    public async Task<ScheduleRunDto> HandleAsync(GetScheduleRunQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var run = await db.ScheduleRuns
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == query.RunId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new EntityNotFoundException($"Schedule run '{query.RunId}' was not found.");

        return ScheduleRunMappings.ToDto(run);
    }
}

using HomeDaemon.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace HomeDaemon.Application.Budget;

/// <summary>
/// Picks the most recent distinct transfer directions. Only current budget participants are
/// considered, so a suggestion is always valid for a new transfer.
/// </summary>
public sealed class GetBudgetTransferSuggestionsHandler(IApplicationDbContext db)
{
    private const int DefaultLimit = 2;
    private const int MaxLimit = 10;

    public async Task<IReadOnlyList<BudgetTransferSuggestionDto>> HandleAsync(
        GetBudgetTransferSuggestionsQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var limit = Math.Clamp(query.Limit ?? DefaultLimit, 1, MaxLimit);

        var participants = await db.HouseholdMembers
            .AsNoTracking()
            .Where(member => member.HouseholdId == query.HouseholdId && member.IsBudgetParticipant)
            .Select(member => new { member.Id, member.Name })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (participants.Count < 2)
        {
            return [];
        }

        var participantIds = participants.Select(member => member.Id).ToList();

        var transfers = await db.BudgetTransfers
            .AsNoTracking()
            .Where(transfer => participantIds.Contains(transfer.FromMemberId)
                && participantIds.Contains(transfer.ToMemberId))
            .Select(transfer => new
            {
                transfer.FromMemberId,
                transfer.ToMemberId,
                transfer.Date,
                transfer.CreatedAtUtc,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var names = participants.ToDictionary(member => member.Id, member => member.Name);

        return transfers
            .GroupBy(transfer => new { transfer.FromMemberId, transfer.ToMemberId })
            .Select(group => new
            {
                group.Key.FromMemberId,
                group.Key.ToMemberId,
                LastDate = group.Max(transfer => transfer.Date),
                LastCreatedAtUtc = group.Max(transfer => transfer.CreatedAtUtc),
            })
            .OrderByDescending(pair => pair.LastDate)
            .ThenByDescending(pair => pair.LastCreatedAtUtc)
            .Take(limit)
            .Select(pair => new BudgetTransferSuggestionDto(
                pair.FromMemberId,
                names.GetValueOrDefault(pair.FromMemberId, string.Empty),
                pair.ToMemberId,
                names.GetValueOrDefault(pair.ToMemberId, string.Empty)))
            .ToList();
    }
}

using System.Globalization;
using HomeDaemon.Application.Abstractions;
using HomeDaemon.Application.Common;
using HomeDaemon.Application.Recurrence;
using HomeDaemon.Domain.Chores;
using HomeDaemon.Domain.Households;
using Microsoft.EntityFrameworkCore;

namespace HomeDaemon.Application.Chores;

/// <summary>
/// Imports a batch of chore definitions and their execution history in one unit of work.
///
/// Imported executions are recorded like Home Assistant callbacks: their source is
/// <see cref="CompletionSource.HomeAssistant"/> and no member or account is attributed. A chore
/// with no history receives one synthetic completion on the date it was created in the source
/// system, which repairs exports where a chore was inserted but never tracked.
/// </summary>
public sealed class ImportChoresHandler(
    IApplicationDbContext db,
    IChoreRecurrenceService recurrence,
    TimeProvider timeProvider)
{
    public const int MaxChores = 5_000;
    public const int MaxCompletions = 50_000;

    public async Task<ImportChoresResult> HandleAsync(
        ImportChoresCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        RequireBatchSize(command);

        var now = timeProvider.GetUtcNow();
        var context = await PrepareAsync(command, cancellationToken).ConfigureAwait(false);
        var imported = 0;
        var skipped = 0;
        var completionsImported = 0;
        var completionsSkipped = 0;

        foreach (var row in command.Chores)
        {
            RequireRow(row);
            var dates = EffectiveCompletionDates(row);

            // The source has no stable external id column in HomeDaemon, so a title is the
            // deduplication key. Re-importing the same export is safe and does not duplicate
            // chores or their history.
            if (!context.KnownTitles.Add(NormalizeTitle(row.Title)))
            {
                skipped++;
                completionsSkipped += dates.Count;
                continue;
            }

            var chore = BuildChore(row, context, dates, now);
            db.Chores.Add(chore);
            AddCompletions(command.HouseholdId, chore, row, dates, now);
            imported++;
            completionsImported += dates.Count;
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new ImportChoresResult(imported, skipped, completionsImported, completionsSkipped);
    }

    private async Task<ImportContext> PrepareAsync(
        ImportChoresCommand command,
        CancellationToken cancellationToken)
    {
        var household = await HouseholdLookup.GetRequiredAsync(db, cancellationToken).ConfigureAwait(false);
        var rooms = await ResolveRoomsAsync(command, timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        var members = await ResolveMembersAsync(command, cancellationToken).ConfigureAwait(false);
        var titles = await db.Chores
            .Where(chore => chore.HouseholdId == command.HouseholdId)
            .Select(chore => chore.Title)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new ImportContext(
            rooms,
            members,
            new HashSet<string>(titles.Select(NormalizeTitle), StringComparer.OrdinalIgnoreCase),
            household.Id,
            HouseholdClock.Today(household, timeProvider));
    }

    private ChoreDefinition BuildChore(
        ImportChoreRow row,
        ImportContext context,
        List<DateOnly> dates,
        DateTimeOffset now)
    {
        var room = ResolveRowRoom(row, context.Rooms);
        var fixedAssigneeId = row.AssignmentPolicy == AssignmentPolicy.SpecificMember
            ? row.FixedAssigneeMemberId
            : null;

        if (row.AssignmentPolicy == AssignmentPolicy.SpecificMember
            && (fixedAssigneeId is not Guid memberId || !context.Members.Contains(memberId)))
        {
            throw new ApplicationValidationException(
                $"Line {Line(row)}: the fixed assignee does not belong to this household.");
        }

        var chore = new ChoreDefinition
        {
            HouseholdId = context.HouseholdId,
            RoomId = room?.Id,
            Title = row.Title.Trim(),
            Description = string.IsNullOrWhiteSpace(row.Description) ? null : row.Description.Trim(),
            IsActive = row.IsActive,
            EstimatedMinutes = row.EstimatedMinutes,
            EffortWeight = row.EffortWeight,
            PreferSameRoomGrouping = row.PreferSameRoomGrouping,
            Frequency = row.Frequency,
            Interval = row.Interval,
            Mode = row.Mode,
            AnchorDate = row.AnchorDate,
            AssignmentPolicy = row.AssignmentPolicy,
            FixedAssigneeMemberId = fixedAssigneeId,
            ScheduleState = row.IsActive ? ChoreScheduleState.Unplanned : ChoreScheduleState.Paused,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        if (dates.Count > 0)
        {
            chore.LastCompletedDate = dates[^1];
            chore.NextOccurrenceDate = recurrence.GetNextAfterCompletion(chore, dates[^1]);
        }
        else
        {
            chore.NextOccurrenceDate = recurrence.GetInitialOccurrence(chore, context.Today);
        }

        return chore;
    }

    private void AddCompletions(
        Guid householdId,
        ChoreDefinition chore,
        ImportChoreRow row,
        List<DateOnly> dates,
        DateTimeOffset now)
    {
        foreach (var date in dates)
        {
            db.CompletionHistory.Add(new CompletionHistory
            {
                HouseholdId = householdId,
                ChoreDefinitionId = chore.Id,
                CompletedDate = date,
                RecordedAtUtc = now,
                Source = CompletionSource.HomeAssistant,
                IdempotencyKey = BuildIdempotencyKey(row, date),
                CompletedByMemberId = null,
                CompletedByUserId = null,
            });
        }
    }

    private static void RequireBatchSize(ImportChoresCommand command)
    {
        if (command.Chores.Count == 0)
        {
            throw new ApplicationValidationException("The file does not contain any chore.");
        }

        if (command.Chores.Count > MaxChores)
        {
            throw new ApplicationValidationException(
                string.Create(CultureInfo.InvariantCulture, $"A single import can contain at most {MaxChores} chores."));
        }

        if (command.Chores.Sum(row => row.Completions.Count) > MaxCompletions)
        {
            throw new ApplicationValidationException(
                string.Create(CultureInfo.InvariantCulture, $"A single import can contain at most {MaxCompletions} completions."));
        }
    }

    private async Task<Dictionary<string, Room?>> ResolveRoomsAsync(
        ImportChoresCommand command,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var existing = await db.Rooms
            .Where(room => room.HouseholdId == command.HouseholdId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var byId = existing.ToDictionary(room => room.Id);
        var byName = new Dictionary<string, Room>(StringComparer.OrdinalIgnoreCase);
        foreach (var room in existing)
        {
            byName[NormalizeKey(room.Name)] = room;
        }

        var nextSortOrder = existing.Count == 0 ? 0 : existing.Max(room => room.SortOrder) + 1;
        var resolved = new Dictionary<string, Room?>(StringComparer.OrdinalIgnoreCase);

        foreach (var mapping in command.Rooms)
        {
            var source = RequireSource(mapping.SourceName, "room");
            var room = ResolveRoom(mapping, command.HouseholdId, byId, byName, nextSortOrder, now, out var created);

            if (!resolved.TryAdd(source, room))
            {
                throw new ApplicationValidationException($"The room '{mapping.SourceName}' is mapped more than once.");
            }

            if (created)
            {
                nextSortOrder++;
            }
        }

        return resolved;
    }

    private Room? ResolveRoom(
        ImportChoreRoomMapping mapping,
        Guid householdId,
        Dictionary<Guid, Room> byId,
        Dictionary<string, Room> byName,
        int sortOrder,
        DateTimeOffset now,
        out bool created)
    {
        created = false;
        var source = NormalizeKey(mapping.SourceName);

        if (mapping.Create && mapping.RoomId is not null)
        {
            throw new ApplicationValidationException(
                $"The room '{mapping.SourceName}' cannot be both mapped and created.");
        }

        if (mapping.Create)
        {
            if (byName.TryGetValue(source, out var existing))
            {
                return existing;
            }

            var room = new Room
            {
                HouseholdId = householdId,
                Name = mapping.SourceName.Trim(),
                SortOrder = sortOrder,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
            };
            db.Rooms.Add(room);
            byName[source] = room;
            created = true;
            return room;
        }

        if (mapping.RoomId is Guid roomId)
        {
            return byId.TryGetValue(roomId, out var room)
                ? room
                : throw new ApplicationValidationException(
                    $"The room '{mapping.SourceName}' does not belong to this household.");
        }

        // No mapping and no creation means the chores in this group have no room.
        return null;
    }

    private async Task<HashSet<Guid>> ResolveMembersAsync(
        ImportChoresCommand command,
        CancellationToken cancellationToken)
    {
        var ids = command.Chores
            .Where(row => row.AssignmentPolicy == AssignmentPolicy.SpecificMember && row.FixedAssigneeMemberId is not null)
            .Select(row => row.FixedAssigneeMemberId!.Value)
            .Distinct()
            .ToList();

        if (ids.Count == 0)
        {
            return [];
        }

        var members = await db.HouseholdMembers
            .AsNoTracking()
            .Where(member => member.HouseholdId == command.HouseholdId && ids.Contains(member.Id))
            .Select(member => member.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return [.. members];
    }

    private static Room? ResolveRowRoom(ImportChoreRow row, Dictionary<string, Room?> rooms)
    {
        var key = NormalizeKey(row.Room);
        if (key.Length == 0)
        {
            return null;
        }

        return rooms.TryGetValue(key, out var room)
            ? room
            : throw new ApplicationValidationException(
                $"Line {Line(row)}: the room '{row.Room}' has no mapping.");
    }

    private static List<DateOnly> EffectiveCompletionDates(ImportChoreRow row)
    {
        var dates = row.Completions
            .Distinct()
            .OrderBy(date => date)
            .ToList();

        if (dates.Count == 0 && row.SyntheticCompletionDate is DateOnly synthetic)
        {
            dates.Add(synthetic);
        }

        return dates;
    }

    private static void RequireRow(ImportChoreRow row)
    {
        if (string.IsNullOrWhiteSpace(row.SourceId))
        {
            throw new ApplicationValidationException(
                $"Line {Line(row)}: the source id is required.");
        }

        ChoreValidation.Validate(row.Title, row.EstimatedMinutes, row.Interval, row.EffortWeight);

        foreach (var date in row.Completions)
        {
            RequireDate(date, row);
        }

        if (row.SyntheticCompletionDate is DateOnly synthetic)
        {
            RequireDate(synthetic, row);
        }

        if (row.Title.Length > 200)
        {
            throw new ApplicationValidationException(
                $"Line {Line(row)}: the title must be 200 characters or fewer.");
        }

        if (row.Description is { Length: > 2_000 })
        {
            throw new ApplicationValidationException(
                $"Line {Line(row)}: the description must be 2 000 characters or fewer.");
        }
    }

    private static void RequireDate(DateOnly date, ImportChoreRow row)
    {
        if (date.Year is < 1900 or > 2200)
        {
            throw new ApplicationValidationException(
                $"Line {Line(row)}: '{date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}' is not a valid completion date.");
        }
    }

    private static string BuildIdempotencyKey(ImportChoreRow row, DateOnly date)
    {
        var key = string.Create(
            CultureInfo.InvariantCulture,
            $"grocy:{row.SourceId.Trim()}:{date:yyyy-MM-dd}");

        return key.Length <= 300 ? key : key[..300];
    }

    private static string Line(ImportChoreRow row) => row.Line.ToString(CultureInfo.InvariantCulture);

    private static string NormalizeTitle(string title) => title.Trim();

    private static string NormalizeKey(string? value) => value?.Trim() ?? string.Empty;

    private static string RequireSource(string? value, string what)
    {
        var source = NormalizeKey(value);
        return source.Length == 0
            ? throw new ApplicationValidationException($"A {what} mapping has an empty name.")
            : source;
    }

    private sealed record ImportContext(
        Dictionary<string, Room?> Rooms,
        HashSet<Guid> Members,
        HashSet<string> KnownTitles,
        Guid HouseholdId,
        DateOnly Today);
}

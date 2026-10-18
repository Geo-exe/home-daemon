using HomeDaemon.Application.Abstractions;
using HomeDaemon.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace HomeDaemon.Application.Households;

public sealed class UpdateMemberHandler(IApplicationDbContext db, TimeProvider timeProvider)
{
    public async Task<HouseholdMemberDto> HandleAsync(UpdateMemberCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (string.IsNullOrWhiteSpace(command.Name))
        {
            throw new ApplicationValidationException("The member name is required.");
        }

        var member = await db.HouseholdMembers
            .FirstOrDefaultAsync(item => item.Id == command.MemberId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new EntityNotFoundException($"Member '{command.MemberId}' was not found.");

        var now = timeProvider.GetUtcNow();
        member.Name = command.Name.Trim();
        member.Color = command.Color;
        member.IsAssignable = command.IsAssignable;
        member.IsBudgetParticipant = command.IsBudgetParticipant;
        member.TodoListEntityId = HouseholdTextHelpers.NullIfWhiteSpace(command.TodoListEntityId);
        member.PersonEntityId = HouseholdTextHelpers.NullIfWhiteSpace(command.PersonEntityId);
        member.UpdatedAtUtc = now;

        var existing = await db.HouseholdMemberCalendars
            .Where(calendar => calendar.HouseholdMemberId == member.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var calendar in existing)
        {
            db.HouseholdMemberCalendars.Remove(calendar);
        }

        HouseholdTextHelpers.AddCalendars(db, member.Id, command.CalendarEntityIds, now);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return HouseholdMappings.ToDto(member, HouseholdTextHelpers.Clean(command.CalendarEntityIds));
    }
}

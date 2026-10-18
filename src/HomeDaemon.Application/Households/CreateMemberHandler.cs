using HomeDaemon.Application.Abstractions;
using HomeDaemon.Application.Common;
using HomeDaemon.Domain.Households;
using Microsoft.EntityFrameworkCore;

namespace HomeDaemon.Application.Households;

public sealed class CreateMemberHandler(IApplicationDbContext db, TimeProvider timeProvider)
{
    public async Task<HouseholdMemberDto> HandleAsync(CreateMemberCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (string.IsNullOrWhiteSpace(command.Name))
        {
            throw new ApplicationValidationException("The member name is required.");
        }

        _ = await db.Households
            .FirstOrDefaultAsync(item => item.Id == command.HouseholdId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new EntityNotFoundException($"Household '{command.HouseholdId}' was not found.");

        var now = timeProvider.GetUtcNow();
        var member = new HouseholdMember
        {
            HouseholdId = command.HouseholdId,
            Name = command.Name.Trim(),
            Color = command.Color,
            IsAssignable = command.IsAssignable,
            TodoListEntityId = HouseholdTextHelpers.NullIfWhiteSpace(command.TodoListEntityId),
            PersonEntityId = HouseholdTextHelpers.NullIfWhiteSpace(command.PersonEntityId),
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        db.HouseholdMembers.Add(member);
        HouseholdTextHelpers.AddCalendars(db, member.Id, command.CalendarEntityIds, now);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return HouseholdMappings.ToDto(member, HouseholdTextHelpers.Clean(command.CalendarEntityIds));
    }
}

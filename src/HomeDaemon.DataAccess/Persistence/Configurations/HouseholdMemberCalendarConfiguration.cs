using HomeDaemon.Domain.Households;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomeDaemon.DataAccess.Persistence.Configurations;

public sealed class HouseholdMemberCalendarConfiguration : IEntityTypeConfiguration<HouseholdMemberCalendar>
{
    public void Configure(EntityTypeBuilder<HouseholdMemberCalendar> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("household_member_calendars");
        builder.HasKey(calendar => calendar.Id);
        builder.Property(calendar => calendar.CalendarEntityId).HasMaxLength(200).IsRequired();

        builder.HasOne<HouseholdMember>()
            .WithMany()
            .HasForeignKey(calendar => calendar.HouseholdMemberId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(calendar => new { calendar.HouseholdMemberId, calendar.CalendarEntityId }).IsUnique();
    }
}

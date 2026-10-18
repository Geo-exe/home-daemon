using HomeDaemon.Domain.Households;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomeDaemon.DataAccess.Persistence.Configurations;

public sealed class HouseholdMemberConfiguration : IEntityTypeConfiguration<HouseholdMember>
{
    public void Configure(EntityTypeBuilder<HouseholdMember> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("household_members");
        builder.HasKey(member => member.Id);
        builder.Property(member => member.Name).HasMaxLength(200).IsRequired();
        builder.Property(member => member.Color).HasMaxLength(32);
        builder.Property(member => member.TodoListEntityId).HasMaxLength(200);
        builder.Property(member => member.PersonEntityId).HasMaxLength(200);

        builder.HasOne<Household>()
            .WithMany()
            .HasForeignKey(member => member.HouseholdId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(member => member.HouseholdId);
    }
}

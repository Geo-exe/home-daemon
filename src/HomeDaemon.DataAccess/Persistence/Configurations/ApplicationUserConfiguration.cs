using HomeDaemon.DataAccess.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomeDaemon.DataAccess.Persistence.Configurations;

public sealed class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Property(user => user.DisplayName).HasMaxLength(200);

        // Existing rows predate the flag, so it defaults to active.
        builder.Property(user => user.IsActive).HasDefaultValue(true);

        // A household member is claimed by at most one account. A partial index keeps the
        // unlinked accounts (the common case) out of the constraint.
        builder.HasIndex(user => user.HouseholdMemberId)
            .IsUnique()
            .HasFilter("\"HouseholdMemberId\" IS NOT NULL");
    }
}

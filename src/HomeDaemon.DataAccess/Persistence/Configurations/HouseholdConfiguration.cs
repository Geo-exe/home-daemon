using HomeDaemon.Domain.Households;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomeDaemon.DataAccess.Persistence.Configurations;

public sealed class HouseholdConfiguration : IEntityTypeConfiguration<Household>
{
    public void Configure(EntityTypeBuilder<Household> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("households");
        builder.HasKey(household => household.Id);
        builder.Property(household => household.Name).HasMaxLength(200).IsRequired();
        builder.Property(household => household.TimeZoneId).HasMaxLength(100).IsRequired();
        builder.Property(household => household.Currency).HasMaxLength(3).IsRequired();
    }
}

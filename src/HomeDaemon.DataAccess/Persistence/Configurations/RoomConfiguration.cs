using HomeDaemon.Domain.Households;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomeDaemon.DataAccess.Persistence.Configurations;

public sealed class RoomConfiguration : IEntityTypeConfiguration<Room>
{
    public void Configure(EntityTypeBuilder<Room> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("rooms");
        builder.HasKey(room => room.Id);
        builder.Property(room => room.Name).HasMaxLength(200).IsRequired();

        builder.HasOne<Household>()
            .WithMany()
            .HasForeignKey(room => room.HouseholdId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(room => room.HouseholdId);
    }
}

using HomeDaemon.Domain.Chores;
using HomeDaemon.Domain.Households;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomeDaemon.DataAccess.Persistence.Configurations;

public sealed class ChoreDefinitionConfiguration : IEntityTypeConfiguration<ChoreDefinition>
{
    public void Configure(EntityTypeBuilder<ChoreDefinition> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("chores");
        builder.HasKey(chore => chore.Id);
        builder.Property(chore => chore.Title).HasMaxLength(200).IsRequired();
        builder.Property(chore => chore.Description).HasMaxLength(2000);
        builder.Property(chore => chore.UnscheduledReason).HasMaxLength(500);

        builder.Property(chore => chore.Frequency).HasConversion<string>().HasMaxLength(32);
        builder.Property(chore => chore.Mode).HasConversion<string>().HasMaxLength(32);
        builder.Property(chore => chore.AssignmentPolicy).HasConversion<string>().HasMaxLength(32);
        builder.Property(chore => chore.ScheduleState).HasConversion<string>().HasMaxLength(32);

        builder.HasOne<Household>()
            .WithMany()
            .HasForeignKey(chore => chore.HouseholdId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Room>()
            .WithMany()
            .HasForeignKey(chore => chore.RoomId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(chore => new { chore.HouseholdId, chore.IsActive, chore.NextOccurrenceDate });
        builder.HasIndex(chore => new { chore.HouseholdId, chore.ScheduleState });

        // Soft delete: deleted chores stay in the database (so completion history keeps its
        // foreign key) but are hidden from every query that does not opt out with
        // IgnoreQueryFilters, for example the history reader.
        builder.HasQueryFilter(chore => chore.DeletedAtUtc == null);
    }
}

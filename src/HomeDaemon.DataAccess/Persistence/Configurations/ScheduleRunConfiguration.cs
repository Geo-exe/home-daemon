using HomeDaemon.Domain.Households;
using HomeDaemon.Domain.Scheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomeDaemon.DataAccess.Persistence.Configurations;

public sealed class ScheduleRunConfiguration : IEntityTypeConfiguration<ScheduleRun>
{
    public void Configure(EntityTypeBuilder<ScheduleRun> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("schedule_runs");
        builder.HasKey(run => run.Id);
        builder.Property(run => run.Trigger).HasConversion<string>().HasMaxLength(32);
        builder.Property(run => run.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(run => run.Summary).HasMaxLength(1000);
        builder.Property(run => run.Diagnostics).HasMaxLength(4000);
        builder.Property(run => run.ErrorMessage).HasMaxLength(2000);

        builder.HasOne<Household>()
            .WithMany()
            .HasForeignKey(run => run.HouseholdId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(run => run.StartedAtUtc);
    }
}

using HomeDaemon.Domain.Chores;
using HomeDaemon.Domain.Households;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomeDaemon.DataAccess.Persistence.Configurations;

public sealed class CompletionHistoryConfiguration : IEntityTypeConfiguration<CompletionHistory>
{
    public void Configure(EntityTypeBuilder<CompletionHistory> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("completion_history");
        builder.HasKey(history => history.Id);
        builder.Property(history => history.Source).HasConversion<string>().HasMaxLength(32);
        builder.Property(history => history.IdempotencyKey).HasMaxLength(300);

        builder.HasOne<Household>()
            .WithMany()
            .HasForeignKey(history => history.HouseholdId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<ChoreDefinition>()
            .WithMany()
            .HasForeignKey(history => history.ChoreDefinitionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(history => new { history.ChoreDefinitionId, history.CompletedDate });
        builder.HasIndex(history => new { history.ChoreDefinitionId, history.IdempotencyKey }).IsUnique();
    }
}

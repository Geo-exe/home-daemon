using HomeDaemon.Domain.Integrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomeDaemon.DataAccess.Persistence.Configurations;

public sealed class HomeAssistantCallLogConfiguration : IEntityTypeConfiguration<HomeAssistantCallLog>
{
    public void Configure(EntityTypeBuilder<HomeAssistantCallLog> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("home_assistant_call_logs");
        builder.HasKey(log => log.Id);
        builder.Property(log => log.Operation).HasMaxLength(100).IsRequired();
        builder.Property(log => log.EntityId).HasMaxLength(200);
        builder.Property(log => log.Method).HasMaxLength(10).IsRequired();
        builder.Property(log => log.ErrorMessage).HasMaxLength(500);

        // The admin page always reads newest first, optionally filtered by outcome.
        builder.HasIndex(log => new { log.OccurredAtUtc, log.Id });
        builder.HasIndex(log => log.Status);
    }
}

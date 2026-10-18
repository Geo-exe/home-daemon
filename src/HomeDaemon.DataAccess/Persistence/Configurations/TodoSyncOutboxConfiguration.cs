using HomeDaemon.Domain.Households;
using HomeDaemon.Domain.Sync;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomeDaemon.DataAccess.Persistence.Configurations;

public sealed class TodoSyncOutboxConfiguration : IEntityTypeConfiguration<TodoSyncOutbox>
{
    public void Configure(EntityTypeBuilder<TodoSyncOutbox> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("todo_sync_outbox");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.OperationType).HasConversion<string>().HasMaxLength(32);
        builder.Property(item => item.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(item => item.TodoListEntityId).HasMaxLength(200).IsRequired();
        builder.Property(item => item.ItemUid).HasMaxLength(300);
        builder.Property(item => item.Payload).HasMaxLength(4000).IsRequired();
        builder.Property(item => item.LastError).HasMaxLength(2000);

        builder.HasOne<Household>()
            .WithMany()
            .HasForeignKey(item => item.HouseholdId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(item => new { item.Status, item.CreatedAtUtc });
    }
}

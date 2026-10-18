using HomeDaemon.Domain.Chores;
using HomeDaemon.Domain.Households;
using HomeDaemon.Domain.Sync;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomeDaemon.DataAccess.Persistence.Configurations;

public sealed class TodoItemLinkConfiguration : IEntityTypeConfiguration<TodoItemLink>
{
    public void Configure(EntityTypeBuilder<TodoItemLink> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("todo_item_links");
        builder.HasKey(link => link.Id);
        builder.Property(link => link.TodoListEntityId).HasMaxLength(200).IsRequired();
        builder.Property(link => link.ItemUid).HasMaxLength(300).IsRequired();

        builder.HasOne<Household>()
            .WithMany()
            .HasForeignKey(link => link.HouseholdId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<ChoreDefinition>()
            .WithMany()
            .HasForeignKey(link => link.ChoreDefinitionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(link => new { link.TodoListEntityId, link.ItemUid }).IsUnique();
        builder.HasIndex(link => link.ChoreDefinitionId).IsUnique();
    }
}

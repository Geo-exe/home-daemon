using HomeDaemon.Domain.Budget;
using HomeDaemon.Domain.Households;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomeDaemon.DataAccess.Persistence.Configurations;

public sealed class BudgetCategoryConfiguration : IEntityTypeConfiguration<BudgetCategory>
{
    public void Configure(EntityTypeBuilder<BudgetCategory> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("budget_categories");
        builder.HasKey(category => category.Id);
        builder.Property(category => category.Name).HasMaxLength(200).IsRequired();

        builder.HasOne<Household>()
            .WithMany()
            .HasForeignKey(category => category.HouseholdId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(category => new { category.HouseholdId, category.SortOrder });
    }
}

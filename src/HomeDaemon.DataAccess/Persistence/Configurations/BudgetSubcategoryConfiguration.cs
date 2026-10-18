using HomeDaemon.Domain.Budget;
using HomeDaemon.Domain.Households;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomeDaemon.DataAccess.Persistence.Configurations;

public sealed class BudgetSubcategoryConfiguration : IEntityTypeConfiguration<BudgetSubcategory>
{
    public void Configure(EntityTypeBuilder<BudgetSubcategory> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("budget_subcategories");
        builder.HasKey(subcategory => subcategory.Id);
        builder.Property(subcategory => subcategory.Name).HasMaxLength(200).IsRequired();

        builder.HasOne<Household>()
            .WithMany()
            .HasForeignKey(subcategory => subcategory.HouseholdId)
            .OnDelete(DeleteBehavior.Cascade);

        // A subcategory is removed only when its category is removed, never on its own.
        builder.HasOne<BudgetCategory>()
            .WithMany()
            .HasForeignKey(subcategory => subcategory.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(subcategory => new { subcategory.HouseholdId, subcategory.CategoryId, subcategory.SortOrder });
    }
}

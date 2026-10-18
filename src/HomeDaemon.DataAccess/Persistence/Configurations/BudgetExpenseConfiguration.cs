using HomeDaemon.Domain.Budget;
using HomeDaemon.Domain.Households;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomeDaemon.DataAccess.Persistence.Configurations;

public sealed class BudgetExpenseConfiguration : IEntityTypeConfiguration<BudgetExpense>
{
    public void Configure(EntityTypeBuilder<BudgetExpense> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("budget_expenses");
        builder.HasKey(expense => expense.Id);
        builder.Property(expense => expense.Description).HasMaxLength(500);

        builder.HasOne<Household>()
            .WithMany()
            .HasForeignKey(expense => expense.HouseholdId)
            .OnDelete(DeleteBehavior.Cascade);

        // Referenced categories, subcategories and people cannot be removed under an expense.
        builder.HasOne<BudgetCategory>()
            .WithMany()
            .HasForeignKey(expense => expense.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<BudgetSubcategory>()
            .WithMany()
            .HasForeignKey(expense => expense.SubcategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<HouseholdMember>()
            .WithMany()
            .HasForeignKey(expense => expense.PaidByMemberId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(expense => new { expense.HouseholdId, expense.Date });
        builder.HasIndex(expense => new { expense.HouseholdId, expense.CategoryId });
        builder.HasIndex(expense => new { expense.HouseholdId, expense.PaidByMemberId });
    }
}

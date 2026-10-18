using HomeDaemon.Domain.Budget;
using HomeDaemon.Domain.Households;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomeDaemon.DataAccess.Persistence.Configurations;

public sealed class BudgetIncomeConfiguration : IEntityTypeConfiguration<BudgetIncome>
{
    public void Configure(EntityTypeBuilder<BudgetIncome> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("budget_incomes");
        builder.HasKey(income => income.Id);

        builder.HasOne<Household>()
            .WithMany()
            .HasForeignKey(income => income.HouseholdId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<HouseholdMember>()
            .WithMany()
            .HasForeignKey(income => income.MemberId)
            .OnDelete(DeleteBehavior.Restrict);

        // One income per person, month and year: the upsert is deterministic.
        builder.HasIndex(income => new { income.HouseholdId, income.MemberId, income.Year, income.Month }).IsUnique();
    }
}

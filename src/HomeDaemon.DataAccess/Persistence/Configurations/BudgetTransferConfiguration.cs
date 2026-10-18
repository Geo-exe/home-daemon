using HomeDaemon.Domain.Budget;
using HomeDaemon.Domain.Households;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HomeDaemon.DataAccess.Persistence.Configurations;

public sealed class BudgetTransferConfiguration : IEntityTypeConfiguration<BudgetTransfer>
{
    public void Configure(EntityTypeBuilder<BudgetTransfer> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("budget_transfers");
        builder.HasKey(transfer => transfer.Id);
        builder.Property(transfer => transfer.Description).HasMaxLength(500);

        builder.HasOne<Household>()
            .WithMany()
            .HasForeignKey(transfer => transfer.HouseholdId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<HouseholdMember>()
            .WithMany()
            .HasForeignKey(transfer => transfer.FromMemberId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<HouseholdMember>()
            .WithMany()
            .HasForeignKey(transfer => transfer.ToMemberId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(transfer => new { transfer.HouseholdId, transfer.SettlementYear, transfer.Date });
    }
}

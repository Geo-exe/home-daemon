using HomeDaemon.Application.Abstractions;
using HomeDaemon.Domain.Budget;
using HomeDaemon.Domain.Chores;
using HomeDaemon.Domain.Households;
using HomeDaemon.Domain.Integrations;
using HomeDaemon.Domain.Scheduling;
using HomeDaemon.Domain.Sync;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace HomeDaemon.DataAccess.Persistence;

/// <summary>
/// The single EF Core context for the application. It also carries the ASP.NET Core Identity
/// tables so user accounts live in the same SQLite file.
/// </summary>
public sealed class HomeDaemonDbContext(DbContextOptions<HomeDaemonDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options), IApplicationDbContext
{
    public DbSet<Household> Households => Set<Household>();

    public DbSet<HouseholdMember> HouseholdMembers => Set<HouseholdMember>();

    public DbSet<HouseholdMemberCalendar> HouseholdMemberCalendars => Set<HouseholdMemberCalendar>();

    public DbSet<Room> Rooms => Set<Room>();

    public DbSet<ChoreDefinition> Chores => Set<ChoreDefinition>();

    public DbSet<CompletionHistory> CompletionHistory => Set<CompletionHistory>();

    public DbSet<TodoItemLink> TodoItemLinks => Set<TodoItemLink>();

    public DbSet<ScheduleRun> ScheduleRuns => Set<ScheduleRun>();

    public DbSet<TodoSyncOutbox> TodoSyncOutbox => Set<TodoSyncOutbox>();

    public DbSet<BudgetCategory> BudgetCategories => Set<BudgetCategory>();

    public DbSet<BudgetSubcategory> BudgetSubcategories => Set<BudgetSubcategory>();

    public DbSet<BudgetIncome> BudgetIncomes => Set<BudgetIncome>();

    public DbSet<BudgetExpense> BudgetExpenses => Set<BudgetExpense>();

    public DbSet<BudgetTransfer> BudgetTransfers => Set<BudgetTransfer>();

    public DbSet<HomeAssistantCallLog> HomeAssistantCallLogs => Set<HomeAssistantCallLog>();

    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken) =>
        Database.BeginTransactionAsync(cancellationToken);

    protected override void OnModelCreating(ModelBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(HomeDaemonDbContext).Assembly);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        ArgumentNullException.ThrowIfNull(configurationBuilder);

        // SQLite stores DateTimeOffset as text, which it cannot order. Encoding it as a
        // binary (ticks based) value keeps ORDER BY and range comparisons working.
        configurationBuilder.Properties<DateTimeOffset>().HaveConversion<DateTimeOffsetToBinaryConverter>();
    }
}

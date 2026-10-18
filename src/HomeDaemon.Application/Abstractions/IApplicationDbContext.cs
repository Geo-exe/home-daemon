using HomeDaemon.Domain.Chores;
using HomeDaemon.Domain.Households;
using HomeDaemon.Domain.Scheduling;
using HomeDaemon.Domain.Sync;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HomeDaemon.Application.Abstractions;

/// <summary>
/// The persistence surface exposed to command and query handlers. It intentionally exposes
/// EF Core abstractions (<see cref="DbSet{TEntity}"/> and transactions) without leaking the
/// concrete <c>DbContext</c>, the SQLite provider or migrations into the application layer.
/// </summary>
public interface IApplicationDbContext
{
    DbSet<Household> Households { get; }

    DbSet<HouseholdMember> HouseholdMembers { get; }

    DbSet<HouseholdMemberCalendar> HouseholdMemberCalendars { get; }

    DbSet<Room> Rooms { get; }

    DbSet<ChoreDefinition> Chores { get; }

    DbSet<CompletionHistory> CompletionHistory { get; }

    DbSet<TodoItemLink> TodoItemLinks { get; }

    DbSet<ScheduleRun> ScheduleRuns { get; }

    DbSet<TodoSyncOutbox> TodoSyncOutbox { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);

    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken);
}

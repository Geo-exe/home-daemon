using System.Globalization;
using HomeDaemon.Application.Budget;
using HomeDaemon.Application.Chores;
using HomeDaemon.Application.Households;
using HomeDaemon.Application.Recurrence;
using HomeDaemon.DataAccess.Persistence;
using HomeDaemon.Domain.Chores;

namespace HomeDaemon.IntegrationTests.Support;

/// <summary>Seeds a household with its core entities through the real application handlers.</summary>
internal static class TestSeed
{
    public static readonly DateOnly DefaultDate = new(2026, 6, 10);

    public static Task<HouseholdDto> HouseholdAsync(HomeDaemonDbContext db, TimeProvider timeProvider, string timeZoneId = "UTC") =>
        new CreateHouseholdHandler(db, timeProvider)
            .HandleAsync(new CreateHouseholdCommand("Home", timeZoneId), CancellationToken.None);

    public static Task<HouseholdMemberDto> MemberAsync(
        HomeDaemonDbContext db,
        TimeProvider timeProvider,
        Guid householdId,
        string name = "Alice",
        bool isAssignable = true,
        bool isBudgetParticipant = false) =>
        new CreateMemberHandler(db, timeProvider).HandleAsync(
            new CreateMemberCommand(householdId, name, null, isAssignable, isBudgetParticipant, $"todo.{name.ToLowerInvariant()}", null, []),
            CancellationToken.None);

    public static Task<RoomDto> RoomAsync(HomeDaemonDbContext db, TimeProvider timeProvider, Guid householdId, string name = "Kitchen") =>
        new CreateRoomHandler(db, timeProvider)
            .HandleAsync(new CreateRoomCommand(householdId, name, 0), CancellationToken.None);

    public static Task<ChoreDetailDto> ChoreAsync(
        HomeDaemonDbContext db,
        TimeProvider timeProvider,
        Guid householdId,
        string title = "Clean oven",
        Guid? roomId = null,
        bool isActive = true) =>
        new CreateChoreHandler(db, new ChoreRecurrenceService(new RecurrenceCalculator()), timeProvider).HandleAsync(
            new CreateChoreCommand(
                householdId,
                title,
                null,
                roomId,
                isActive,
                30,
                null,
                false,
                RecurrenceFrequency.Daily,
                1,
                RecurrenceMode.Dynamic,
                null,
                AssignmentPolicy.Both,
                null),
            CancellationToken.None);

    public static async Task AddCompletionAsync(
        HomeDaemonDbContext db,
        Guid householdId,
        Guid choreId,
        DateOnly completedDate,
        DateTimeOffset recordedAtUtc,
        Guid? memberId = null,
        CompletionSource source = CompletionSource.Manual)
    {
        db.CompletionHistory.Add(new CompletionHistory
        {
            HouseholdId = householdId,
            ChoreDefinitionId = choreId,
            CompletedDate = completedDate,
            RecordedAtUtc = recordedAtUtc,
            Source = source,
            IdempotencyKey = string.Create(
                CultureInfo.InvariantCulture,
                $"{choreId}:{completedDate:yyyy-MM-dd}:{recordedAtUtc.UtcTicks}"),
            CompletedByMemberId = memberId,
        });

        await db.SaveChangesAsync(CancellationToken.None);
    }

    public static Task<BudgetCategoryDto> BudgetCategoryAsync(
        HomeDaemonDbContext db,
        TimeProvider timeProvider,
        Guid householdId,
        string name = "Food") =>
        new CreateBudgetCategoryHandler(db, timeProvider)
            .HandleAsync(new CreateBudgetCategoryCommand(householdId, name, 0), CancellationToken.None);

    public static Task<BudgetSubcategoryDto> BudgetSubcategoryAsync(
        HomeDaemonDbContext db,
        TimeProvider timeProvider,
        Guid householdId,
        Guid categoryId,
        string name = "Groceries") =>
        new CreateBudgetSubcategoryHandler(db, timeProvider)
            .HandleAsync(new CreateBudgetSubcategoryCommand(householdId, categoryId, name, 0), CancellationToken.None);

    public static Task<BudgetExpenseDto> BudgetExpenseAsync(
        HomeDaemonDbContext db,
        TimeProvider timeProvider,
        Guid householdId,
        DateOnly date,
        long amountMinor,
        Guid categoryId,
        Guid paidByMemberId,
        Guid? subcategoryId = null) =>
        new CreateBudgetExpenseHandler(db, timeProvider)
            .HandleAsync(
                new CreateBudgetExpenseCommand(householdId, date, amountMinor, categoryId, subcategoryId, paidByMemberId, null),
                CancellationToken.None);

    public static Task<BudgetIncomeMonthDto> BudgetIncomeAsync(
        HomeDaemonDbContext db,
        TimeProvider timeProvider,
        Guid householdId,
        Guid memberId,
        int year,
        int month,
        long amountMinor) =>
        new UpsertBudgetIncomeHandler(db, timeProvider)
            .HandleAsync(new UpsertBudgetIncomeCommand(householdId, memberId, year, month, amountMinor), CancellationToken.None);
}

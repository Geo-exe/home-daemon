using HomeDaemon.Application.Abstractions;
using HomeDaemon.Application.Budget;
using HomeDaemon.Application.Chores;
using HomeDaemon.Application.Households;
using HomeDaemon.Application.Integrations.HomeAssistant;
using HomeDaemon.Application.Planner;
using HomeDaemon.Application.Recurrence;
using HomeDaemon.Application.Scheduling;
using HomeDaemon.Application.Sync;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HomeDaemon.Application;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IRecurrenceCalculator, RecurrenceCalculator>();
        services.AddSingleton<IChoreRecurrenceService, ChoreRecurrenceService>();
        services.AddSingleton<IRecalculationGate, RecalculationGate>();
        services.AddSingleton<IScheduleRunDispatcher, ScheduleRunDispatcher>();

        services.Configure<SchedulingOptions>(configuration.GetSection("Scheduling"));
        services.Configure<TodoSyncOptions>(configuration.GetSection("TodoSync"));

        services.AddScoped<AvailabilityMatrixBuilder>();
        services.AddScoped<ChoreSyncService>();
        services.AddScoped<ChoreCompletionService>();
        services.AddScoped<TodoSyncProcessor>();
        services.AddScoped<RecalculationExecutor>();

        AddHouseholdHandlers(services);
        AddChoreHandlers(services);
        AddPlannerHandlers(services);
        AddSchedulingHandlers(services);
        AddBudgetHandlers(services);
        AddHomeAssistantHandlers(services);

        return services;
    }

    private static void AddHomeAssistantHandlers(IServiceCollection services)
    {
        services.AddScoped<GetHomeAssistantCallLogsHandler>();
        services.AddScoped<CleanupHomeAssistantCallLogsHandler>();
    }

    private static void AddHouseholdHandlers(IServiceCollection services)
    {
        services.AddScoped<CreateHouseholdHandler>();
        services.AddScoped<UpdateHouseholdHandler>();
        services.AddScoped<GetHouseholdHandler>();
        services.AddScoped<CreateMemberHandler>();
        services.AddScoped<UpdateMemberHandler>();
        services.AddScoped<DeleteMemberHandler>();
        services.AddScoped<GetMembersHandler>();
        services.AddScoped<CreateRoomHandler>();
        services.AddScoped<UpdateRoomHandler>();
        services.AddScoped<DeleteRoomHandler>();
        services.AddScoped<GetRoomsHandler>();
    }

    private static void AddChoreHandlers(IServiceCollection services)
    {
        services.AddScoped<CreateChoreHandler>();
        services.AddScoped<UpdateChoreHandler>();
        services.AddScoped<DeleteChoreHandler>();
        services.AddScoped<RestoreChoreHandler>();
        services.AddScoped<CompleteChoreHandler>();
        services.AddScoped<RecordHomeAssistantCompletionHandler>();
        services.AddScoped<GetChoresHandler>();
        services.AddScoped<GetChoreHandler>();
        services.AddScoped<GetChoreHistoryHandler>();
    }

    private static void AddPlannerHandlers(IServiceCollection services)
    {
        services.AddScoped<GetPlannerHandler>();
        services.AddScoped<UpdateOccurrenceHandler>();
        services.AddScoped<UnlockOccurrenceHandler>();
    }

    private static void AddSchedulingHandlers(IServiceCollection services)
    {
        services.AddScoped<RecalculateScheduleCommandHandler>();
        services.AddScoped<GetScheduleRunHandler>();
        services.AddScoped<GetScheduleRunsHandler>();
    }

    private static void AddBudgetHandlers(IServiceCollection services)
    {
        services.AddScoped<GetBudgetCategoriesHandler>();
        services.AddScoped<CreateBudgetCategoryHandler>();
        services.AddScoped<UpdateBudgetCategoryHandler>();
        services.AddScoped<DeleteBudgetCategoryHandler>();
        services.AddScoped<CreateBudgetSubcategoryHandler>();
        services.AddScoped<UpdateBudgetSubcategoryHandler>();
        services.AddScoped<DeleteBudgetSubcategoryHandler>();

        services.AddScoped<GetBudgetIncomesHandler>();
        services.AddScoped<UpsertBudgetIncomeHandler>();
        services.AddScoped<DeleteBudgetIncomeHandler>();

        services.AddScoped<GetBudgetExpensesHandler>();
        services.AddScoped<GetBudgetExpenseHandler>();
        services.AddScoped<CreateBudgetExpenseHandler>();
        services.AddScoped<ImportBudgetExpensesHandler>();
        services.AddScoped<UpdateBudgetExpenseHandler>();
        services.AddScoped<DeleteBudgetExpenseHandler>();

        services.AddScoped<GetBudgetTransfersHandler>();
        services.AddScoped<GetBudgetTransferSuggestionsHandler>();
        services.AddScoped<CreateBudgetTransferHandler>();
        services.AddScoped<UpdateBudgetTransferHandler>();
        services.AddScoped<DeleteBudgetTransferHandler>();

        services.AddScoped<GetBudgetSummaryHandler>();
        services.AddScoped<GetBudgetYearsHandler>();
    }
}

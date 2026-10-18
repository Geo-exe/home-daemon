using HomeDaemon.Application.Abstractions;
using HomeDaemon.Application.Chores;
using HomeDaemon.Application.Households;
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

        return services;
    }

    private static void AddHouseholdHandlers(IServiceCollection services)
    {
        services.AddScoped<CreateHouseholdHandler>();
        services.AddScoped<UpdateHouseholdHandler>();
        services.AddScoped<GetHouseholdHandler>();
        services.AddScoped<CreateMemberHandler>();
        services.AddScoped<UpdateMemberHandler>();
        services.AddScoped<GetMembersHandler>();
        services.AddScoped<CreateRoomHandler>();
        services.AddScoped<GetRoomsHandler>();
    }

    private static void AddChoreHandlers(IServiceCollection services)
    {
        services.AddScoped<CreateChoreHandler>();
        services.AddScoped<UpdateChoreHandler>();
        services.AddScoped<DeleteChoreHandler>();
        services.AddScoped<CompleteChoreHandler>();
        services.AddScoped<RecordHomeAssistantCompletionHandler>();
        services.AddScoped<GetChoresHandler>();
        services.AddScoped<GetChoreHandler>();
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
    }
}

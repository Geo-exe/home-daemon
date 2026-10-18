using HomeDaemon.Domain.Scheduling;

namespace HomeDaemon.Application.Scheduling;

public sealed record RecalculateScheduleCommand(ScheduleRunTrigger Trigger);

namespace HomeDaemon.Application.Scheduling;

/// <summary>A calendar event already normalized for availability math.</summary>
public sealed record CalendarEvent(string? Summary, DateTimeOffset StartsAt, DateTimeOffset EndsAt, bool IsAllDay);

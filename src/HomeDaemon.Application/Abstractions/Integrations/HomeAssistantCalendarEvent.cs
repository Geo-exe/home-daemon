namespace HomeDaemon.Application.Abstractions.Integrations;

/// <summary>
/// A calendar event read from Home Assistant. When <paramref name="IsAllDay"/> is true the
/// event blocks the whole local day; otherwise only the overlap with the scheduling window
/// is subtracted.
/// </summary>
public sealed record HomeAssistantCalendarEvent(string? Summary, DateTimeOffset StartsAt, DateTimeOffset EndsAt, bool IsAllDay);

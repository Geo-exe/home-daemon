namespace HomeDaemon.Application.Households;

/// <summary>
/// Creates the single household. <paramref name="Currency"/> is optional; when it is null or
/// blank the household keeps the default currency (EUR).
/// </summary>
public sealed record CreateHouseholdCommand(string Name, string TimeZoneId, string? Currency = null);

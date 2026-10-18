namespace HomeDaemon.Api.Endpoints;

/// <summary>
/// Creates the single household. <paramref name="Currency"/> is optional so older clients keep
/// working; when it is omitted the handler applies the default currency.
/// </summary>
public sealed record CreateHouseholdRequest(string Name, string TimeZoneId, string? Currency = null);

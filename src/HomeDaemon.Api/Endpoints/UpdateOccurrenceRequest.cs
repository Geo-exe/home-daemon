namespace HomeDaemon.Api.Endpoints;

public sealed record UpdateOccurrenceRequest(DateOnly Date, Guid MemberId);

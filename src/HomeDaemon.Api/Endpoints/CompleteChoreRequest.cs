namespace HomeDaemon.Api.Endpoints;

/// <summary>
/// A manual completion. The account and, when linked, the household member are resolved from
/// the session, so the client cannot claim a different person.
/// </summary>
public sealed record CompleteChoreRequest(DateOnly? CompletedDate, string? IdempotencyKey);

namespace HomeDaemon.Api.Endpoints;

public sealed record CompleteChoreRequest(DateOnly? CompletedDate, Guid? CompletedByMemberId, string? IdempotencyKey);

namespace HomeDaemon.Application.Sync;

/// <summary>Payload stored in the outbox and sent to Home Assistant.</summary>
public sealed record TodoSyncPayload(string Summary, string? Description, DateOnly? DueDate);

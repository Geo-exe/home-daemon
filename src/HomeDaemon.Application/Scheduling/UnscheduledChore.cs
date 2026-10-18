namespace HomeDaemon.Application.Scheduling;

/// <summary>A candidate that could not be placed, with an observable reason.</summary>
public sealed record UnscheduledChore(Guid ChoreId, string Reason);

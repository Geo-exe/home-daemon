namespace HomeDaemon.Api.Endpoints;

public sealed record ImportChoresResponse(
    int Imported,
    int Skipped,
    int CompletionsImported,
    int CompletionsSkipped,
    Guid? RecalculationRunId);

namespace HomeDaemon.Application.Chores;

/// <summary>
/// <paramref name="Imported"/> counts new chores and <paramref name="Skipped"/> existing titles.
/// Completion counters make a re-import observable without duplicating history.
/// </summary>
public sealed record ImportChoresResult(
    int Imported,
    int Skipped,
    int CompletionsImported,
    int CompletionsSkipped);

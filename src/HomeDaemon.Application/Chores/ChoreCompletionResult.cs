namespace HomeDaemon.Application.Chores;

public sealed record ChoreCompletionResult(Guid ChoreId, bool IsDuplicate);

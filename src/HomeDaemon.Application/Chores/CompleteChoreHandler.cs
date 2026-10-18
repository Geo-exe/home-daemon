namespace HomeDaemon.Application.Chores;

public sealed class CompleteChoreHandler(ChoreCompletionService completion)
{
    public Task<ChoreCompletionResult> HandleAsync(CompleteChoreCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return completion.CompleteAsync(command.ChoreId, command.CompletedDate, command.CompletedByMemberId, command.Source, command.IdempotencyKey, cancellationToken);
    }
}

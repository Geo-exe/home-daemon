using HomeDaemon.Domain.Sync;
using Microsoft.Extensions.Logging;

namespace HomeDaemon.Application.Sync;

/// <summary>Source-generated, allocation-free log messages for the to-do sync worker.</summary>
internal static partial class TodoSyncLog
{
    [LoggerMessage(EventId = 5001, Level = LogLevel.Error, Message = "Home Assistant rejected sync operation {OperationId} ({OperationType}): {Message}")]
    public static partial void OperationRejected(ILogger logger, Guid operationId, TodoSyncOperationType operationType, string message);

    [LoggerMessage(EventId = 5002, Level = LogLevel.Error, Message = "Sync operation {OperationId} ({OperationType}) violated a database constraint: {Message}")]
    public static partial void ConstraintViolation(ILogger logger, Guid operationId, TodoSyncOperationType operationType, string message);

    [LoggerMessage(EventId = 5003, Level = LogLevel.Warning, Message = "Home Assistant sync operation {OperationId} ({OperationType}) failed on attempt {Attempts}: {Message}")]
    public static partial void TransientFailure(ILogger logger, Guid operationId, TodoSyncOperationType operationType, int attempts, string message);
}

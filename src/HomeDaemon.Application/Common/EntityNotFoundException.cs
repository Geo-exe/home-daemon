namespace HomeDaemon.Application.Common;

/// <summary>Raised when a command or query references an entity that does not exist.</summary>
public sealed class EntityNotFoundException : Exception
{
    public EntityNotFoundException()
    {
    }

    public EntityNotFoundException(string message)
        : base(message)
    {
    }

    public EntityNotFoundException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

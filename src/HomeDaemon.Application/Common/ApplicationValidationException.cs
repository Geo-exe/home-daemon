namespace HomeDaemon.Application.Common;

/// <summary>
/// Raised when a command payload violates an application-level rule. It is mapped to a
/// 400 response by the API and never leaks internal details.
/// </summary>
public sealed class ApplicationValidationException : Exception
{
    public ApplicationValidationException()
    {
    }

    public ApplicationValidationException(string message)
        : base(message)
    {
    }

    public ApplicationValidationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

namespace HomeDaemon.Api.Auth;

public sealed class ApiKeyOptions
{
    public const string HeaderName = "X-Api-Key";

    /// <summary>Machine credential used by Home Assistant to call HomeDaemon.</summary>
    public string? Value { get; set; }
}

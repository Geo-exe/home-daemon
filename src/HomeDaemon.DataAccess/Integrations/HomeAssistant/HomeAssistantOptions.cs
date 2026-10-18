namespace HomeDaemon.DataAccess.Integrations.HomeAssistant;

public sealed class HomeAssistantOptions
{
    /// <summary>Base URL of the Home Assistant instance, for example <c>http://homeassistant.local:8123/</c>.</summary>
    public Uri? BaseUrl { get; set; }

    /// <summary>Long-lived access token used by the backend to call Home Assistant.</summary>
    public string? Token { get; set; }

    public int TimeoutSeconds { get; set; } = 30;
}

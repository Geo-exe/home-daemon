namespace HomeDaemon.Api.Auth;

public static class ApiKeyDefaults
{
    public const string Scheme = "HomeAssistantApiKey";
    public const string PolicyScheme = "MultiScheme";

    /// <summary>Claim type written on the principal when the machine credential is accepted.</summary>
    public const string MachineClaimType = "homedaaemon:client";
}

namespace HomeDaemon.Api;

/// <summary>
/// Names of the configuration section and keys that control the forwarded headers middleware.
/// See <see cref="ForwardedHeadersServiceCollectionExtensions"/> for the behaviour.
/// </summary>
public static class ForwardedHeadersSettings
{
    /// <summary>Configuration section bound to these keys.</summary>
    public const string SectionName = "ForwardedHeaders";

    /// <summary>Whether the proxy's <c>X-Forwarded-*</c> headers are processed at all.</summary>
    public const string EnabledKey = "Enabled";

    /// <summary>Exact addresses of the trusted proxies.</summary>
    public const string KnownProxiesKey = "KnownProxies";

    /// <summary>CIDR ranges of the trusted proxies, for example <c>172.18.0.0/16</c>.</summary>
    public const string KnownNetworksKey = "KnownNetworks";
}

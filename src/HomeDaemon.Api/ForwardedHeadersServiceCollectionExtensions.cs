using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

// .NET 10 moves the trusted networks to KnownIPNetworks; the obsolete HttpOverrides.IPNetwork
// would otherwise clash with this type.
using IPNetwork = System.Net.IPNetwork;

namespace HomeDaemon.Api;

/// <summary>
/// Wires up the forwarded headers middleware so the app can run behind a TLS-terminating reverse
/// proxy (for example Tailscale Serve) and still see the original <c>https</c> scheme and client
/// address. The feature is opt-in and, when enabled, trusts only the proxies or networks the
/// operator lists; it never clears the trusted lists.
/// </summary>
public static class ForwardedHeadersServiceCollectionExtensions
{
    public static IServiceCollection AddForwardedHeaders(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var section = configuration.GetSection(ForwardedHeadersSettings.SectionName);
        var enabled = section.GetValue<bool>(ForwardedHeadersSettings.EnabledKey);
        var knownProxies = ParseProxies(section.GetSection(ForwardedHeadersSettings.KnownProxiesKey).Get<string[]>() ?? []);
        var knownNetworks = ParseNetworks(section.GetSection(ForwardedHeadersSettings.KnownNetworksKey).Get<string[]>() ?? []);

        if (enabled && knownProxies.Count == 0 && knownNetworks.Count == 0)
        {
            throw new InvalidOperationException(
                $"Set at least one address in {ForwardedHeadersSettings.SectionName}:{ForwardedHeadersSettings.KnownProxiesKey} "
                + $"or {ForwardedHeadersSettings.SectionName}:{ForwardedHeadersSettings.KnownNetworksKey} when forwarded headers are enabled.");
        }

        // The options are always registered because UseForwardedHeaders resolves them from the
        // container. When the feature is off the middleware sees ForwardedHeaders.None.
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            if (!enabled)
            {
                return;
            }

            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

            // A single trusted hop: the reverse proxy directly in front of the container.
            options.ForwardLimit = 1;

            foreach (var proxy in knownProxies)
            {
                options.KnownProxies.Add(proxy);
            }

            foreach (var network in knownNetworks)
            {
                options.KnownIPNetworks.Add(network);
            }
        });

        return services;
    }

    private static List<IPAddress> ParseProxies(IEnumerable<string> values)
    {
        List<IPAddress> proxies = [];

        foreach (var value in values)
        {
            if (!IPAddress.TryParse(value, out var address))
            {
                throw new InvalidOperationException(
                    $"'{value}' is not a valid IP address in {ForwardedHeadersSettings.SectionName}:{ForwardedHeadersSettings.KnownProxiesKey}.");
            }

            proxies.Add(address);
        }

        return proxies;
    }

    private static List<IPNetwork> ParseNetworks(IEnumerable<string> values)
    {
        List<IPNetwork> networks = [];

        foreach (var value in values)
        {
            if (!IPNetwork.TryParse(value, out var network))
            {
                throw new InvalidOperationException(
                    $"'{value}' is not a valid CIDR network in {ForwardedHeadersSettings.SectionName}:{ForwardedHeadersSettings.KnownNetworksKey}.");
            }

            networks.Add(network);
        }

        return networks;
    }
}

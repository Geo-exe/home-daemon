using System.Net;
using HomeDaemon.Api;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using IPNetwork = System.Net.IPNetwork;

namespace HomeDaemon.IntegrationTests;

/// <summary>
/// Covers the forwarded headers configuration: off by default, opt-in behind a proxy, trusting
/// only what the operator lists and failing fast on a broken setup.
/// </summary>
public sealed class ForwardedHeadersSetupTests
{
    [Fact]
    public void DisabledByDefaultLeavesForwardingOff()
    {
        var options = ResolveOptions([]);

        options.ForwardedHeaders.Should().Be(ForwardedHeaders.None);
    }

    [Fact]
    public void EnabledTrustsTheConfiguredProxy()
    {
        var options = ResolveOptions(new Dictionary<string, string?>
        {
            ["ForwardedHeaders:Enabled"] = "true",
            ["ForwardedHeaders:KnownProxies:0"] = "172.18.0.1",
        });

        options.ForwardedHeaders.Should().Be(ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto);
        options.ForwardLimit.Should().Be(1);
        options.KnownProxies.Should().Contain(IPAddress.Parse("172.18.0.1"));
    }

    [Fact]
    public void EnabledTrustsTheConfiguredNetwork()
    {
        var options = ResolveOptions(new Dictionary<string, string?>
        {
            ["ForwardedHeaders:Enabled"] = "true",
            ["ForwardedHeaders:KnownNetworks:0"] = "172.18.0.0/16",
        });

        options.KnownIPNetworks.Should().Contain(IPNetwork.Parse("172.18.0.0/16"));
    }

    [Fact]
    public void EnabledWithoutATrustedProxyIsRejected()
    {
        var configure = () => ResolveOptions(new Dictionary<string, string?>
        {
            ["ForwardedHeaders:Enabled"] = "true",
        });

        configure.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void InvalidProxyIsRejected()
    {
        var configure = () => ResolveOptions(new Dictionary<string, string?>
        {
            ["ForwardedHeaders:Enabled"] = "true",
            ["ForwardedHeaders:KnownProxies:0"] = "not-an-address",
        });

        configure.Should().Throw<InvalidOperationException>();
    }

    private static ForwardedHeadersOptions ResolveOptions(Dictionary<string, string?> values)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();

        var services = new ServiceCollection();
        services.AddForwardedHeaders(configuration);

        using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;
    }
}

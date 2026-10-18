using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace HomeDaemon.Api.Auth;

/// <summary>
/// Authenticates Home Assistant calls with a dedicated machine credential sent in a header,
/// never in the URL. User accounts use the Identity bearer scheme instead.
/// </summary>
public sealed class ApiKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IOptions<ApiKeyOptions> apiKeyOptions)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(ApiKeyOptions.HeaderName, out var provided))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var expected = apiKeyOptions.Value.Value;
        if (string.IsNullOrWhiteSpace(expected))
        {
            return Task.FromResult(AuthenticateResult.Fail("The Home Assistant API key is not configured."));
        }

        if (!IsMatch(provided.ToString(), expected))
        {
            return Task.FromResult(AuthenticateResult.Fail("The provided API key is not valid."));
        }

        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.Name, "home-assistant"),
                new Claim(ApiKeyDefaults.MachineClaimType, "home-assistant"),
            ],
            Scheme.Name);

        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    private static bool IsMatch(string provided, string expected)
    {
        var providedBytes = Encoding.UTF8.GetBytes(provided);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(providedBytes, expectedBytes);
    }
}

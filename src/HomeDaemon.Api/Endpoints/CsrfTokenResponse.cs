namespace HomeDaemon.Api.Endpoints;

/// <summary>The request token the client must echo in the antiforgery header on mutations.</summary>
public sealed record CsrfTokenResponse(string Token);

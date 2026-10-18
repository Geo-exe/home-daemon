namespace HomeDaemon.Api.Endpoints;

public sealed record RecalculationAcceptedResponse(Guid RunId, string Status);

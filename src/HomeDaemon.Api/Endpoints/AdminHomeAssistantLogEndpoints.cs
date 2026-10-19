using HomeDaemon.Api.Auth;
using HomeDaemon.Application.Integrations.HomeAssistant;
using HomeDaemon.Domain.Integrations;

namespace HomeDaemon.Api.Endpoints;

/// <summary>
/// Administrator-only diagnostics for outbound Home Assistant calls. The token and the request
/// payloads are never part of the recorded data.
/// </summary>
public static class AdminHomeAssistantLogEndpoints
{
    public static IEndpointRouteBuilder MapAdminHomeAssistantLogEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/home-assistant-logs")
            .WithTags("Admin")
            .RequireAuthorization(AccountRoles.AdminPolicy);

        group.MapGet("/", ListAsync);
        return group;
    }

    private static async Task<IResult> ListAsync(
        HomeAssistantCallStatus? status,
        string? operation,
        string? entityId,
        string? cursor,
        int? pageSize,
        GetHomeAssistantCallLogsHandler handler,
        CancellationToken cancellationToken)
    {
        var page = await handler.HandleAsync(
            new GetHomeAssistantCallLogsQuery(status, operation, entityId, cursor, pageSize),
            cancellationToken).ConfigureAwait(false);

        return Results.Ok(page);
    }
}

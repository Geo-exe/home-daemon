namespace HomeDaemon.Api.Endpoints;

public static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/health/live", () => Results.Ok(new { status = "Healthy" }))
            .AllowAnonymous()
            .WithTags("Health");

        app.MapHealthChecks("/health/ready")
            .AllowAnonymous()
            .WithTags("Health");

        app.MapHealthChecks("/health")
            .AllowAnonymous()
            .WithTags("Health");

        return app;
    }
}

namespace HomeDaemon.Api.Endpoints;

public sealed record ImportSubcategoryMappingRequest(
    string CategorySourceName,
    string SourceName,
    Guid? SubcategoryId,
    bool Create);

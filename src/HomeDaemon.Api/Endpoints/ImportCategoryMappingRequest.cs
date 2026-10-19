namespace HomeDaemon.Api.Endpoints;

public sealed record ImportCategoryMappingRequest(string SourceName, Guid? CategoryId, bool Create);

namespace HomeDaemon.Api.Endpoints;

public sealed record UpdateBudgetSubcategoryRequest(string Name, int SortOrder, bool IsArchived);

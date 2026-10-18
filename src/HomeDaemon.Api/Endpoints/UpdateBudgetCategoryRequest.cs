namespace HomeDaemon.Api.Endpoints;

public sealed record UpdateBudgetCategoryRequest(string Name, int SortOrder, bool IsArchived);

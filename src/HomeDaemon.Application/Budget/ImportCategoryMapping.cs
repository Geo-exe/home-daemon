namespace HomeDaemon.Application.Budget;

/// <summary>
/// How one category name found in the file is resolved. Exactly one of
/// <paramref name="CategoryId"/> or <paramref name="Create"/> must be used.
/// </summary>
public sealed record ImportCategoryMapping(string SourceName, Guid? CategoryId, bool Create);

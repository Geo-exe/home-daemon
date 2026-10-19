namespace HomeDaemon.Application.Budget;

/// <summary>How one category/subcategory pair found in the file is resolved.</summary>
public sealed record ImportSubcategoryMapping(
    string CategorySourceName,
    string SourceName,
    Guid? SubcategoryId,
    bool Create);

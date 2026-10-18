using HomeDaemon.Application.Budget;
using HomeDaemon.Application.Households;

namespace HomeDaemon.Api.Endpoints;

/// <summary>
/// The budget module API. The household is always resolved from the session, so a client can
/// never address another household's data; writes go through the CSRF check like every other
/// mutating endpoint.
/// </summary>
public static class BudgetEndpoints
{
    public static IEndpointRouteBuilder MapBudgetEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/budget").WithTags("Budget");
        MapSummary(group);
        MapCategories(group);
        MapIncomes(group);
        MapExpenses(group);
        MapTransfers(group);
        return group;
    }

    private static void MapSummary(RouteGroupBuilder group)
    {
        group.MapGet("/summary", GetSummaryAsync);
        group.MapGet("/years", GetYearsAsync);
    }

    private static void MapCategories(RouteGroupBuilder group)
    {
        group.MapGet("/categories", ListCategoriesAsync);
        group.MapPost("/categories", CreateCategoryAsync);
        group.MapPut("/categories/{categoryId:guid}", UpdateCategoryAsync);
        group.MapDelete("/categories/{categoryId:guid}", DeleteCategoryAsync);
        group.MapPost("/categories/{categoryId:guid}/subcategories", CreateSubcategoryAsync);
        group.MapPut("/subcategories/{subcategoryId:guid}", UpdateSubcategoryAsync);
        group.MapDelete("/subcategories/{subcategoryId:guid}", DeleteSubcategoryAsync);
    }

    private static void MapIncomes(RouteGroupBuilder group)
    {
        group.MapGet("/incomes", GetIncomesAsync);
        group.MapPut("/incomes", UpsertIncomeAsync);
        group.MapDelete("/incomes/{incomeId:guid}", DeleteIncomeAsync);
    }

    private static void MapExpenses(RouteGroupBuilder group)
    {
        group.MapGet("/expenses", ListExpensesAsync);
        group.MapPost("/expenses", CreateExpenseAsync);
        group.MapGet("/expenses/{expenseId:guid}", GetExpenseAsync);
        group.MapPut("/expenses/{expenseId:guid}", UpdateExpenseAsync);
        group.MapDelete("/expenses/{expenseId:guid}", DeleteExpenseAsync);
    }

    private static void MapTransfers(RouteGroupBuilder group)
    {
        group.MapGet("/transfers", ListTransfersAsync);
        group.MapPost("/transfers", CreateTransferAsync);
        group.MapPut("/transfers/{transferId:guid}", UpdateTransferAsync);
        group.MapDelete("/transfers/{transferId:guid}", DeleteTransferAsync);
    }

    private static async Task<IResult> GetSummaryAsync(
        int year,
        GetHouseholdHandler getHousehold,
        GetBudgetSummaryHandler handler,
        CancellationToken cancellationToken)
    {
        var household = await getHousehold.HandleAsync(new GetHouseholdQuery(), cancellationToken).ConfigureAwait(false);
        return Results.Ok(await handler.HandleAsync(new GetBudgetSummaryQuery(household.Id, year), cancellationToken).ConfigureAwait(false));
    }

    private static async Task<IResult> GetYearsAsync(
        GetHouseholdHandler getHousehold,
        GetBudgetYearsHandler handler,
        CancellationToken cancellationToken)
    {
        var household = await getHousehold.HandleAsync(new GetHouseholdQuery(), cancellationToken).ConfigureAwait(false);
        return Results.Ok(await handler.HandleAsync(new GetBudgetYearsQuery(household.Id), cancellationToken).ConfigureAwait(false));
    }

    private static async Task<IResult> ListCategoriesAsync(
        GetHouseholdHandler getHousehold,
        GetBudgetCategoriesHandler handler,
        CancellationToken cancellationToken)
    {
        var household = await getHousehold.HandleAsync(new GetHouseholdQuery(), cancellationToken).ConfigureAwait(false);
        return Results.Ok(await handler.HandleAsync(new GetBudgetCategoriesQuery(household.Id), cancellationToken).ConfigureAwait(false));
    }

    private static async Task<IResult> CreateCategoryAsync(
        CreateBudgetCategoryRequest request,
        GetHouseholdHandler getHousehold,
        CreateBudgetCategoryHandler handler,
        CancellationToken cancellationToken)
    {
        var household = await getHousehold.HandleAsync(new GetHouseholdQuery(), cancellationToken).ConfigureAwait(false);
        var created = await handler.HandleAsync(
            new CreateBudgetCategoryCommand(household.Id, request.Name, request.SortOrder),
            cancellationToken).ConfigureAwait(false);
        return Results.Created($"/api/v1/budget/categories/{created.Id}", created);
    }

    private static async Task<IResult> UpdateCategoryAsync(
        Guid categoryId,
        UpdateBudgetCategoryRequest request,
        GetHouseholdHandler getHousehold,
        UpdateBudgetCategoryHandler handler,
        CancellationToken cancellationToken)
    {
        var household = await getHousehold.HandleAsync(new GetHouseholdQuery(), cancellationToken).ConfigureAwait(false);
        return Results.Ok(await handler.HandleAsync(
            new UpdateBudgetCategoryCommand(household.Id, categoryId, request.Name, request.SortOrder, request.IsArchived),
            cancellationToken).ConfigureAwait(false));
    }

    private static async Task<IResult> DeleteCategoryAsync(
        Guid categoryId,
        GetHouseholdHandler getHousehold,
        DeleteBudgetCategoryHandler handler,
        CancellationToken cancellationToken)
    {
        var household = await getHousehold.HandleAsync(new GetHouseholdQuery(), cancellationToken).ConfigureAwait(false);
        await handler.HandleAsync(new DeleteBudgetCategoryCommand(household.Id, categoryId), cancellationToken).ConfigureAwait(false);
        return Results.NoContent();
    }

    private static async Task<IResult> CreateSubcategoryAsync(
        Guid categoryId,
        CreateBudgetSubcategoryRequest request,
        GetHouseholdHandler getHousehold,
        CreateBudgetSubcategoryHandler handler,
        CancellationToken cancellationToken)
    {
        var household = await getHousehold.HandleAsync(new GetHouseholdQuery(), cancellationToken).ConfigureAwait(false);
        var created = await handler.HandleAsync(
            new CreateBudgetSubcategoryCommand(household.Id, categoryId, request.Name, request.SortOrder),
            cancellationToken).ConfigureAwait(false);
        return Results.Created($"/api/v1/budget/subcategories/{created.Id}", created);
    }

    private static async Task<IResult> UpdateSubcategoryAsync(
        Guid subcategoryId,
        UpdateBudgetSubcategoryRequest request,
        GetHouseholdHandler getHousehold,
        UpdateBudgetSubcategoryHandler handler,
        CancellationToken cancellationToken)
    {
        var household = await getHousehold.HandleAsync(new GetHouseholdQuery(), cancellationToken).ConfigureAwait(false);
        return Results.Ok(await handler.HandleAsync(
            new UpdateBudgetSubcategoryCommand(household.Id, subcategoryId, request.Name, request.SortOrder, request.IsArchived),
            cancellationToken).ConfigureAwait(false));
    }

    private static async Task<IResult> DeleteSubcategoryAsync(
        Guid subcategoryId,
        GetHouseholdHandler getHousehold,
        DeleteBudgetSubcategoryHandler handler,
        CancellationToken cancellationToken)
    {
        var household = await getHousehold.HandleAsync(new GetHouseholdQuery(), cancellationToken).ConfigureAwait(false);
        await handler.HandleAsync(new DeleteBudgetSubcategoryCommand(household.Id, subcategoryId), cancellationToken).ConfigureAwait(false);
        return Results.NoContent();
    }

    private static async Task<IResult> GetIncomesAsync(
        int year,
        GetHouseholdHandler getHousehold,
        GetBudgetIncomesHandler handler,
        CancellationToken cancellationToken)
    {
        var household = await getHousehold.HandleAsync(new GetHouseholdQuery(), cancellationToken).ConfigureAwait(false);
        return Results.Ok(await handler.HandleAsync(new GetBudgetIncomesQuery(household.Id, year), cancellationToken).ConfigureAwait(false));
    }

    private static async Task<IResult> UpsertIncomeAsync(
        UpsertBudgetIncomeRequest request,
        GetHouseholdHandler getHousehold,
        UpsertBudgetIncomeHandler handler,
        CancellationToken cancellationToken)
    {
        var household = await getHousehold.HandleAsync(new GetHouseholdQuery(), cancellationToken).ConfigureAwait(false);
        return Results.Ok(await handler.HandleAsync(
            new UpsertBudgetIncomeCommand(household.Id, request.MemberId, request.Year, request.Month, request.AmountMinor),
            cancellationToken).ConfigureAwait(false));
    }

    private static async Task<IResult> DeleteIncomeAsync(
        Guid incomeId,
        GetHouseholdHandler getHousehold,
        DeleteBudgetIncomeHandler handler,
        CancellationToken cancellationToken)
    {
        var household = await getHousehold.HandleAsync(new GetHouseholdQuery(), cancellationToken).ConfigureAwait(false);
        await handler.HandleAsync(new DeleteBudgetIncomeCommand(household.Id, incomeId), cancellationToken).ConfigureAwait(false);
        return Results.NoContent();
    }

    private static async Task<IResult> ListExpensesAsync(
        int? year,
        DateOnly? from,
        DateOnly? to,
        Guid? categoryId,
        Guid? subcategoryId,
        Guid? memberId,
        string? cursor,
        int? pageSize,
        GetHouseholdHandler getHousehold,
        GetBudgetExpensesHandler handler,
        CancellationToken cancellationToken)
    {
        var household = await getHousehold.HandleAsync(new GetHouseholdQuery(), cancellationToken).ConfigureAwait(false);
        return Results.Ok(await handler.HandleAsync(
            new GetBudgetExpensesQuery(household.Id, year, from, to, categoryId, subcategoryId, memberId, cursor, pageSize),
            cancellationToken).ConfigureAwait(false));
    }

    private static async Task<IResult> CreateExpenseAsync(
        CreateBudgetExpenseRequest request,
        GetHouseholdHandler getHousehold,
        CreateBudgetExpenseHandler handler,
        CancellationToken cancellationToken)
    {
        var household = await getHousehold.HandleAsync(new GetHouseholdQuery(), cancellationToken).ConfigureAwait(false);
        var created = await handler.HandleAsync(
            new CreateBudgetExpenseCommand(
                household.Id,
                request.Date,
                request.AmountMinor,
                request.CategoryId,
                request.SubcategoryId,
                request.PaidByMemberId,
                request.Description),
            cancellationToken).ConfigureAwait(false);
        return Results.Created($"/api/v1/budget/expenses/{created.Id}", created);
    }

    private static async Task<IResult> GetExpenseAsync(
        Guid expenseId,
        GetHouseholdHandler getHousehold,
        GetBudgetExpenseHandler handler,
        CancellationToken cancellationToken)
    {
        var household = await getHousehold.HandleAsync(new GetHouseholdQuery(), cancellationToken).ConfigureAwait(false);
        return Results.Ok(await handler.HandleAsync(new GetBudgetExpenseQuery(household.Id, expenseId), cancellationToken).ConfigureAwait(false));
    }

    private static async Task<IResult> UpdateExpenseAsync(
        Guid expenseId,
        UpdateBudgetExpenseRequest request,
        GetHouseholdHandler getHousehold,
        UpdateBudgetExpenseHandler handler,
        CancellationToken cancellationToken)
    {
        var household = await getHousehold.HandleAsync(new GetHouseholdQuery(), cancellationToken).ConfigureAwait(false);
        return Results.Ok(await handler.HandleAsync(
            new UpdateBudgetExpenseCommand(
                household.Id,
                expenseId,
                request.Date,
                request.AmountMinor,
                request.CategoryId,
                request.SubcategoryId,
                request.PaidByMemberId,
                request.Description),
            cancellationToken).ConfigureAwait(false));
    }

    private static async Task<IResult> DeleteExpenseAsync(
        Guid expenseId,
        GetHouseholdHandler getHousehold,
        DeleteBudgetExpenseHandler handler,
        CancellationToken cancellationToken)
    {
        var household = await getHousehold.HandleAsync(new GetHouseholdQuery(), cancellationToken).ConfigureAwait(false);
        await handler.HandleAsync(new DeleteBudgetExpenseCommand(household.Id, expenseId), cancellationToken).ConfigureAwait(false);
        return Results.NoContent();
    }

    private static async Task<IResult> ListTransfersAsync(
        int? settlementYear,
        string? cursor,
        int? pageSize,
        GetHouseholdHandler getHousehold,
        GetBudgetTransfersHandler handler,
        CancellationToken cancellationToken)
    {
        var household = await getHousehold.HandleAsync(new GetHouseholdQuery(), cancellationToken).ConfigureAwait(false);
        return Results.Ok(await handler.HandleAsync(
            new GetBudgetTransfersQuery(household.Id, settlementYear, cursor, pageSize),
            cancellationToken).ConfigureAwait(false));
    }

    private static async Task<IResult> CreateTransferAsync(
        CreateBudgetTransferRequest request,
        GetHouseholdHandler getHousehold,
        CreateBudgetTransferHandler handler,
        CancellationToken cancellationToken)
    {
        var household = await getHousehold.HandleAsync(new GetHouseholdQuery(), cancellationToken).ConfigureAwait(false);
        var created = await handler.HandleAsync(
            new CreateBudgetTransferCommand(
                household.Id,
                request.Date,
                request.SettlementYear,
                request.AmountMinor,
                request.FromMemberId,
                request.ToMemberId,
                request.Description),
            cancellationToken).ConfigureAwait(false);
        return Results.Created($"/api/v1/budget/transfers/{created.Id}", created);
    }

    private static async Task<IResult> UpdateTransferAsync(
        Guid transferId,
        UpdateBudgetTransferRequest request,
        GetHouseholdHandler getHousehold,
        UpdateBudgetTransferHandler handler,
        CancellationToken cancellationToken)
    {
        var household = await getHousehold.HandleAsync(new GetHouseholdQuery(), cancellationToken).ConfigureAwait(false);
        return Results.Ok(await handler.HandleAsync(
            new UpdateBudgetTransferCommand(
                household.Id,
                transferId,
                request.Date,
                request.SettlementYear,
                request.AmountMinor,
                request.FromMemberId,
                request.ToMemberId,
                request.Description),
            cancellationToken).ConfigureAwait(false));
    }

    private static async Task<IResult> DeleteTransferAsync(
        Guid transferId,
        GetHouseholdHandler getHousehold,
        DeleteBudgetTransferHandler handler,
        CancellationToken cancellationToken)
    {
        var household = await getHousehold.HandleAsync(new GetHouseholdQuery(), cancellationToken).ConfigureAwait(false);
        await handler.HandleAsync(new DeleteBudgetTransferCommand(household.Id, transferId), cancellationToken).ConfigureAwait(false);
        return Results.NoContent();
    }
}

namespace HomeDaemon.Application.Common;

/// <summary>
/// A cursor-paginated page. Keyset pagination is used instead of offset paging so that new
/// rows inserted while the client is scrolling do not shift or duplicate the following pages.
/// </summary>
/// <typeparam name="T">The item type of the page.</typeparam>
/// <param name="Items">The items of this page, in the query's stable order.</param>
/// <param name="NextCursor">Opaque cursor to pass back for the next page, or null on the last page.</param>
/// <param name="HasMore">Whether at least one more item follows after this page.</param>
public sealed record CursorPageDto<T>(
    IReadOnlyList<T> Items,
    string? NextCursor,
    bool HasMore);

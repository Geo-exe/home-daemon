namespace HomeDaemon.Domain.Sync;

/// <summary>
/// The mutation HomeDaemon wants Home Assistant to apply to a to-do item.
/// The numeric order doubles as the dispatch order: a delete must be applied before a
/// create/update for the same chore so an assignee change does not duplicate the item.
/// </summary>
public enum TodoSyncOperationType
{
    Delete = 0,
    Create = 1,
    Update = 2,
}

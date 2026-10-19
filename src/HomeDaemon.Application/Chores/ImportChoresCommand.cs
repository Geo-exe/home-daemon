namespace HomeDaemon.Application.Chores;

public sealed record ImportChoresCommand(
    Guid HouseholdId,
    IReadOnlyList<ImportChoreRow> Chores,
    IReadOnlyList<ImportChoreRoomMapping> Rooms);

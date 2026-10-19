namespace HomeDaemon.Api.Endpoints;

public sealed record ImportChoresRequest(
    IReadOnlyList<ImportChoreRowRequest>? Chores,
    IReadOnlyList<ImportChoreRoomMappingRequest>? Rooms);

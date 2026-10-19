namespace HomeDaemon.Api.Endpoints;

public sealed record ImportChoreRoomMappingRequest(
    string SourceName,
    Guid? RoomId,
    bool Create);

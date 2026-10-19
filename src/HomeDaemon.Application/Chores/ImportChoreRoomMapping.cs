namespace HomeDaemon.Application.Chores;

/// <summary>
/// Resolves one room name found in the file. With <paramref name="Create"/>, the room is
/// created when missing; with a null <paramref name="RoomId"/> and no creation, the chores are
/// imported without a room.
/// </summary>
public sealed record ImportChoreRoomMapping(string SourceName, Guid? RoomId, bool Create);

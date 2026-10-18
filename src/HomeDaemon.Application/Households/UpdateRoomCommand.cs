namespace HomeDaemon.Application.Households;

public sealed record UpdateRoomCommand(Guid RoomId, string Name, int SortOrder);

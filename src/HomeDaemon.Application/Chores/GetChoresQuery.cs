namespace HomeDaemon.Application.Chores;

public sealed record GetChoresQuery(Guid HouseholdId, Guid? RoomId, bool? IsActive);

namespace HomeDaemon.Application.Households;

public sealed record CreateRoomCommand(Guid HouseholdId, string Name, int SortOrder);

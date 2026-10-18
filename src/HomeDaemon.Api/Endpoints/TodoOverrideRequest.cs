namespace HomeDaemon.Api.Endpoints;

public sealed record TodoOverrideRequest(string TodoListEntityId, string ItemUid, DateOnly Date, Guid? MemberId);

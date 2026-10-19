using System.Net;
using System.Net.Http.Json;
using HomeDaemon.Application.Chores;
using HomeDaemon.Application.Households;
using HomeDaemon.Application.Recurrence;
using HomeDaemon.DataAccess.Persistence;
using HomeDaemon.Domain.Chores;
using HomeDaemon.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HomeDaemon.IntegrationTests;

/// <summary>
/// The endpoints the Home Assistant custom integration relies on: the authenticated
/// handshake it uses to validate URL and API key, and the to-do completion callback.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class HomeAssistantHandshakeTests
{
    private readonly HomeDaemonApiFactory _factory;

    public HomeAssistantHandshakeTests(HomeDaemonApiFactory factory) => _factory = factory;

    [Fact]
    public async Task HandshakeRequiresTheMachineKey()
    {
        using var client = _factory.CreateClient();

        var anonymous = await client.GetAsync(
            "/api/v1/integrations/home-assistant/handshake",
            TestContext.Current.CancellationToken);

        anonymous.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task HandshakeReturnsTheHouseholdForAValidKey()
    {
        await _factory.EnsureSeededAsync();

        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/integrations/home-assistant/handshake");
        request.Headers.TryAddWithoutValidation("X-Api-Key", HomeDaemonApiFactory.ApiKey);

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<HandshakeDto>(TestContext.Current.CancellationToken);
        payload!.HouseholdId.Should().NotBeEmpty();
        payload.HouseholdName.Should().Be("Test Home");
        payload.Version.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task CompletionForAnUnmanagedListIsDiscarded()
    {
        await _factory.EnsureSeededAsync();

        var response = await PostCompletionAsync($"todo.unmanaged-{Guid.NewGuid():N}", "unmanaged-item", idempotencyKey: null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task CompletionAttributesTheMemberWhoseListOwnsTheItem()
    {
        await _factory.EnsureSeededAsync();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HomeDaemonDbContext>();
        var time = scope.ServiceProvider.GetRequiredService<TimeProvider>();
        var household = await db.Households.AsNoTracking().FirstAsync(TestContext.Current.CancellationToken);

        var list = $"todo.member-{Guid.NewGuid():N}";
        var member = await new CreateMemberHandler(db, time).HandleAsync(
            new CreateMemberCommand(household.Id, "Widget Owner", null, true, false, list, null, []),
            CancellationToken.None);

        var chore = await new CreateChoreHandler(db, new ChoreRecurrenceService(new RecurrenceCalculator()), time).HandleAsync(
            new CreateChoreCommand(household.Id, $"Widget chore {Guid.NewGuid():N}", null, null, true, 15, null, false, RecurrenceFrequency.Daily, 1, RecurrenceMode.Dynamic, null, AssignmentPolicy.Both, null),
            CancellationToken.None);

        var itemUid = $"uid-{Guid.NewGuid():N}";
        db.TodoItemLinks.Add(new TodoItemLink
        {
            HouseholdId = household.Id,
            ChoreDefinitionId = chore.Id,
            TodoListEntityId = list,
            ItemUid = itemUid,
            CreatedAtUtc = time.GetUtcNow(),
            UpdatedAtUtc = time.GetUtcNow(),
        });

        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var response = await PostCompletionAsync(list, itemUid, idempotencyKey: $"integration-{Guid.NewGuid():N}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var history = await db.CompletionHistory
            .AsNoTracking()
            .Where(item => item.ChoreDefinitionId == chore.Id)
            .OrderByDescending(item => item.RecordedAtUtc)
            .FirstAsync(TestContext.Current.CancellationToken);

        history.Source.Should().Be(CompletionSource.HomeAssistant);
        history.CompletedByMemberId.Should().Be(member.Id);
    }

    private async Task<HttpResponseMessage> PostCompletionAsync(string list, string itemUid, string? idempotencyKey)
    {
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/integrations/home-assistant/todo-completions")
        {
            Content = JsonContent.Create(new
            {
                todoListEntityId = list,
                itemUid,
                completedByMemberId = (Guid?)null,
                idempotencyKey,
            }),
        };
        request.Headers.TryAddWithoutValidation("X-Api-Key", HomeDaemonApiFactory.ApiKey);

        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private sealed record HandshakeDto(Guid HouseholdId, string HouseholdName, string Version);
}

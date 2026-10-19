using System.Net;
using System.Net.Http.Json;
using HomeDaemon.Application.Abstractions.Integrations;
using HomeDaemon.DataAccess.Integrations.HomeAssistant;
using HomeDaemon.IntegrationTests.Support;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace HomeDaemon.IntegrationTests;

/// <summary>
/// The entity picker endpoint requires a session and never leaks the Home Assistant token.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class HomeAssistantEndpointTests
{
    private readonly HomeDaemonApiFactory _factory;

    public HomeAssistantEndpointTests(HomeDaemonApiFactory factory) => _factory = factory;

    [Fact]
    public async Task EntityListRequiresAuthentication()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync(
            "/api/v1/integrations/home-assistant/entities",
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task EntityListReturnsThePickerDomainsFromTheIntegration()
    {
        using var admin = await _factory.SignInAsAdminAsync();

        using var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IHomeAssistantClient>();
                services.AddSingleton<IHomeAssistantClient>(new StubHomeAssistantClient());
                services.Configure<HomeAssistantOptions>(options => options.BaseUrl = new Uri("http://homeassistant.test/"));
            });
        });

        using var client = factory.CreateClient();
        var login = await HomeDaemonApiFactory.PostAsync(client, "/api/v1/auth/login", new
        {
            email = HomeDaemonApiFactory.AdminEmail,
            password = HomeDaemonApiFactory.AdminPassword,
        });
        login.EnsureSuccessStatusCode();

        var payload = await client.GetFromJsonAsync<HomeAssistantEntitiesDto>(
            "/api/v1/integrations/home-assistant/entities",
            TestContext.Current.CancellationToken);

        payload!.Available.Should().BeTrue();
        payload.Entities.Select(entity => entity.EntityId)
            .Should().Equal("person.alice", "todo.shopping", "calendar.work");
    }

    private sealed class StubHomeAssistantClient : IHomeAssistantClient
    {
        public Task<IReadOnlyList<HomeAssistantEntity>> GetEntitiesAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<HomeAssistantEntity>>(
            [
                new HomeAssistantEntity("person.alice", "person", "Alice"),
                new HomeAssistantEntity("todo.shopping", "todo", "Shopping"),
                new HomeAssistantEntity("calendar.work", "calendar", "Work"),
            ]);

        public Task<IReadOnlyList<HomeAssistantTodoItem>> GetTodoItemsAsync(string todoListEntityId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<HomeAssistantTodoItem>>([]);

        public Task<HomeAssistantTodoItem> CreateTodoItemAsync(string todoListEntityId, HomeAssistantTodoItemDraft draft, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task UpdateTodoItemAsync(string todoListEntityId, string itemUid, HomeAssistantTodoItemDraft draft, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task DeleteTodoItemAsync(string todoListEntityId, string itemUid, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<HomeAssistantCalendarEvent>> GetCalendarEventsAsync(string calendarEntityId, DateOnly fromInclusive, DateOnly toExclusive, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<HomeAssistantCalendarEvent>>([]);
    }
}

internal sealed record HomeAssistantEntitiesDto(bool Available, IReadOnlyList<HomeAssistantEntityDto> Entities);

internal sealed record HomeAssistantEntityDto(string EntityId, string Domain, string? FriendlyName);

using System.Net;
using HomeDaemon.Application.Abstractions.Integrations;
using HomeDaemon.DataAccess.Integrations.HomeAssistant;

namespace HomeDaemon.UnitTests.Integrations;

public sealed class HttpHomeAssistantClientTests
{
    [Fact]
    public async Task EntityListKeepsOnlyThePickerDomainsAndSortsThem()
    {
        using var handler = new StubHandler("""
            [
              { "entity_id": "sensor.kitchen", "attributes": { "friendly_name": "Kitchen" } },
              { "entity_id": "todo.shopping", "attributes": { "friendly_name": "Shopping" } },
              { "entity_id": "person.alice", "attributes": { "friendly_name": "Alice" } },
              { "entity_id": "calendar.work", "attributes": { "friendly_name": "Work" } }
            ]
            """);
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://homeassistant.test/") };
        var client = new HttpHomeAssistantClient(httpClient);

        var entities = await client.GetEntitiesAsync(TestContext.Current.CancellationToken);

        entities.Should().HaveCount(3);
        entities.Select(entity => entity.EntityId).Should().Equal("calendar.work", "person.alice", "todo.shopping");
        entities.Should().OnlyContain(entity => entity.FriendlyName != null);
        handler.LastRequestUri.Should().Be("http://homeassistant.test/api/states");
    }

    [Fact]
    public async Task UnexpectedPayloadIsReportedAsAPermanentFailure()
    {
        using var handler = new StubHandler("""{ "not": "a list" }""");
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://homeassistant.test/") };
        var client = new HttpHomeAssistantClient(httpClient);

        var action = () => client.GetEntitiesAsync(TestContext.Current.CancellationToken);

        var exception = await action.Should().ThrowAsync<HomeAssistantRequestException>();
        exception.Which.IsTransient.Should().BeFalse();
    }

    private sealed class StubHandler(string payload) : HttpMessageHandler
    {
        public string? LastRequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri?.ToString();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(payload),
            });
        }
    }
}

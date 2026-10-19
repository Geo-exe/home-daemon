using System.Net;
using HomeDaemon.Application.Abstractions.Integrations;
using HomeDaemon.DataAccess.Integrations.HomeAssistant;

namespace HomeDaemon.UnitTests.Integrations;

/// <summary>
/// The Home Assistant adapter reports every outbound call to the diagnostic recorder without
/// ever changing the call's own behaviour.
/// </summary>
public sealed class HttpHomeAssistantClientLoggingTests
{
    [Fact]
    public async Task ASuccessfulCallIsRecorded()
    {
        using var handler = new StubHandler(HttpStatusCode.OK, """{ "service_response": { "todo.alice": { "items": [] } } }""");
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://homeassistant.test/") };
        var recorder = new RecordingCallRecorder();
        var client = new HttpHomeAssistantClient(httpClient, recorder);

        await client.GetTodoItemsAsync("todo.alice", TestContext.Current.CancellationToken);

        var record = recorder.Records.Should().ContainSingle().Subject;
        record.Operation.Should().Be("todo.get_items");
        record.EntityId.Should().Be("todo.alice");
        record.Succeeded.Should().BeTrue();
        record.StatusCode.Should().Be(200);
        record.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task AFailedCallIsRecordedWithItsStatus()
    {
        using var handler = new StubHandler(HttpStatusCode.ServiceUnavailable, "{}");
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://homeassistant.test/") };
        var recorder = new RecordingCallRecorder();
        var client = new HttpHomeAssistantClient(httpClient, recorder);

        var action = () => client.GetCalendarEventsAsync(
            "calendar.work",
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 1, 8),
            TestContext.Current.CancellationToken);

        await action.Should().ThrowAsync<HomeAssistantRequestException>();

        var record = recorder.Records.Should().ContainSingle().Subject;
        record.Operation.Should().Be("calendar.get_events");
        record.EntityId.Should().Be("calendar.work");
        record.Succeeded.Should().BeFalse();
        record.StatusCode.Should().Be(503);
        record.IsTransient.Should().BeTrue();
        record.ErrorMessage.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ARecorderThatThrowsNeverBreaksTheCall()
    {
        using var handler = new StubHandler(HttpStatusCode.OK, """{ "service_response": { "todo.alice": { "items": [] } } }""");
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://homeassistant.test/") };
        var client = new HttpHomeAssistantClient(httpClient, new ThrowingCallRecorder());

        var items = await client.GetTodoItemsAsync("todo.alice", TestContext.Current.CancellationToken);

        items.Should().BeEmpty();
    }

    private sealed class StubHandler(HttpStatusCode status, string payload) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(payload) });
    }

    private sealed class RecordingCallRecorder : IHomeAssistantCallRecorder
    {
        public List<HomeAssistantCallRecord> Records { get; } = [];

        public void Record(HomeAssistantCallRecord record) => Records.Add(record);
    }

    private sealed class ThrowingCallRecorder : IHomeAssistantCallRecorder
    {
        public void Record(HomeAssistantCallRecord record) => throw new InvalidOperationException("recorder down");
    }
}

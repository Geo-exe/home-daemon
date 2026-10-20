using System.Net;
using System.Text.Json;
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
    public async Task AFailedCallRecordsTheDetailReturnedByHomeAssistant()
    {
        using var handler = new StubHandler(
            HttpStatusCode.BadRequest,
            """{ "message": "Item is not supported by this to-do list" }""");
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://homeassistant.test/") };
        var recorder = new RecordingCallRecorder();
        var client = new HttpHomeAssistantClient(httpClient, recorder);

        var action = () => client.GetTodoItemsAsync("todo.fra_s_chores", TestContext.Current.CancellationToken);

        await action.Should().ThrowAsync<HomeAssistantRequestException>();

        var record = recorder.Records.Should().ContainSingle().Subject;
        record.Succeeded.Should().BeFalse();
        record.StatusCode.Should().Be(400);
        record.IsTransient.Should().BeFalse();
        record.ErrorMessage.Should().Contain("Item is not supported by this to-do list");
        record.ErrorMessage.Should().NotContain("Bearer");
    }

    [Fact]
    public async Task AddItemDoesNotSendStatusButUpdateDoes()
    {
        using var handler = new CapturingHandler();
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://homeassistant.test/") };
        var client = new HttpHomeAssistantClient(httpClient);
        var dueDate = new DateOnly(2026, 4, 10);

        await client.CreateTodoItemAsync(
            "todo.alice",
            new HomeAssistantTodoItemDraft("Clean kitchen", null, dueDate, HomeAssistantTodoStatus.NeedsAction),
            TestContext.Current.CancellationToken);

        await client.UpdateTodoItemAsync(
            "todo.alice",
            "uid-1",
            new HomeAssistantTodoItemDraft("Clean kitchen", null, dueDate, HomeAssistantTodoStatus.Completed),
            TestContext.Current.CancellationToken);

        var addBody = handler.BodyFor("todo/add_item");
        addBody.Should().NotBeNull();
        using (var addDocument = JsonDocument.Parse(addBody!))
        {
            addDocument.RootElement.TryGetProperty("status", out _).Should().BeFalse();
            addDocument.RootElement.GetProperty("item").GetString().Should().Be("Clean kitchen");
        }

        var updateBody = handler.BodyFor("todo/update_item");
        updateBody.Should().NotBeNull();
        using var updateDocument = JsonDocument.Parse(updateBody!);
        updateDocument.RootElement.GetProperty("status").GetString().Should().Be("completed");
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

    private sealed class CapturingHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, string> _bodies = new(StringComparer.Ordinal);

        public string? BodyFor(string pathFragment) =>
            _bodies.FirstOrDefault(entry => entry.Key.Contains(pathFragment, StringComparison.Ordinal)).Value;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;
            if (request.Content is not null)
            {
                _bodies[path] = await request.Content.ReadAsStringAsync(cancellationToken);
            }

            var payload = path.Contains("todo/get_items", StringComparison.Ordinal)
                ? """{ "service_response": { "todo.alice": { "items": [ { "uid": "uid-1", "summary": "Clean kitchen", "status": "needs_action", "due": "2026-04-10" } ] } } }"""
                : "{}";

            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(payload) };
        }
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

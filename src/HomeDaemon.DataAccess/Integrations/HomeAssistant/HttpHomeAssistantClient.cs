using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using HomeDaemon.Application.Abstractions.Integrations;

namespace HomeDaemon.DataAccess.Integrations.HomeAssistant;

/// <summary>
/// REST adapter for Home Assistant to-do lists and calendars. It only uses the documented
/// service actions (<c>todo.add_item</c>, <c>todo.update_item</c>, <c>todo.remove_item</c>,
/// <c>todo.get_items</c>) and the calendar REST endpoint, and it never treats item titles as
/// identifiers. Every call is optionally reported to the diagnostic recorder.
/// </summary>
public sealed class HttpHomeAssistantClient(
    HttpClient httpClient,
    IHomeAssistantCallRecorder? callRecorder = null) : IHomeAssistantClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private static readonly HashSet<string> PickerDomains = new(StringComparer.Ordinal)
    {
        "person",
        "todo",
        "calendar",
    };

    public Task<IReadOnlyList<HomeAssistantEntity>> GetEntitiesAsync(CancellationToken cancellationToken) =>
        TrackAsync("states.list", null, HttpMethod.Get.Method, () => GetEntitiesCoreAsync(cancellationToken), cancellationToken);

    private async Task<IReadOnlyList<HomeAssistantEntity>> GetEntitiesCoreAsync(CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Get, "api/states", content: null, cancellationToken).ConfigureAwait(false);
        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            throw new HomeAssistantRequestException("Home Assistant returned an unexpected state list.", isTransient: false);
        }

        var entities = new List<HomeAssistantEntity>();
        foreach (var element in document.RootElement.EnumerateArray())
        {
            var entityId = GetString(element, "entity_id");
            if (string.IsNullOrWhiteSpace(entityId))
            {
                continue;
            }

            var separator = entityId.IndexOf('.', StringComparison.Ordinal);
            var domain = separator > 0 ? entityId[..separator] : string.Empty;
            if (!PickerDomains.Contains(domain))
            {
                continue;
            }

            var friendlyName = element.TryGetProperty("attributes", out var attributes)
                && attributes.ValueKind == JsonValueKind.Object
                ? GetString(attributes, "friendly_name")
                : null;

            entities.Add(new HomeAssistantEntity(entityId, domain, friendlyName));
        }

        return entities
            .OrderBy(entity => entity.Domain, StringComparer.Ordinal)
            .ThenBy(entity => entity.FriendlyName ?? entity.EntityId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entity => entity.EntityId, StringComparer.Ordinal)
            .ToList();
    }

    public Task<IReadOnlyList<HomeAssistantTodoItem>> GetTodoItemsAsync(string todoListEntityId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(todoListEntityId);
        return TrackAsync("todo.get_items", todoListEntityId, HttpMethod.Post.Method, () => GetTodoItemsCoreAsync(todoListEntityId, cancellationToken), cancellationToken);
    }

    private async Task<IReadOnlyList<HomeAssistantTodoItem>> GetTodoItemsCoreAsync(string todoListEntityId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(todoListEntityId);
        var body = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["entity_id"] = todoListEntityId,
            ["status"] = new[] { "needs_action", "completed" },
        };

        using var response = await PostServiceAsync("todo/get_items", body, returnResponse: true, cancellationToken).ConfigureAwait(false);
        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return ParseItems(json, todoListEntityId);
    }

    public Task<HomeAssistantTodoItem> CreateTodoItemAsync(string todoListEntityId, HomeAssistantTodoItemDraft draft, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(todoListEntityId);
        ArgumentNullException.ThrowIfNull(draft);
        return TrackAsync("todo.add_item", todoListEntityId, HttpMethod.Post.Method, () => CreateTodoItemCoreAsync(todoListEntityId, draft, cancellationToken), cancellationToken);
    }

    private async Task<HomeAssistantTodoItem> CreateTodoItemCoreAsync(string todoListEntityId, HomeAssistantTodoItemDraft draft, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(todoListEntityId);
        ArgumentNullException.ThrowIfNull(draft);

        var body = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["entity_id"] = todoListEntityId,
            ["item"] = draft.Summary,
        };
        AddOptionalFields(body, draft, includeRename: false);

        using var response = await PostServiceAsync("todo/add_item", body, returnResponse: false, cancellationToken).ConfigureAwait(false);

        var items = await GetTodoItemsAsync(todoListEntityId, cancellationToken).ConfigureAwait(false);
        var matches = items
            .Where(item => string.Equals(item.Summary, draft.Summary, StringComparison.Ordinal) && item.DueDate == draft.DueDate)
            .ToList();

        return matches.Count switch
        {
            1 => matches[0],
            0 => throw new HomeAssistantRequestException(
                $"The item created on '{todoListEntityId}' could not be located afterwards.",
                isTransient: false),
            _ => throw new HomeAssistantRequestException(
                $"The item created on '{todoListEntityId}' could not be identified uniquely.",
                isTransient: false),
        };
    }

    public Task UpdateTodoItemAsync(string todoListEntityId, string itemUid, HomeAssistantTodoItemDraft draft, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(todoListEntityId);
        ArgumentNullException.ThrowIfNull(itemUid);
        ArgumentNullException.ThrowIfNull(draft);
        return TrackAsync("todo.update_item", todoListEntityId, HttpMethod.Post.Method, () => UpdateTodoItemCoreAsync(todoListEntityId, itemUid, draft, cancellationToken), cancellationToken);
    }

    private async Task UpdateTodoItemCoreAsync(string todoListEntityId, string itemUid, HomeAssistantTodoItemDraft draft, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(todoListEntityId);
        ArgumentNullException.ThrowIfNull(itemUid);
        ArgumentNullException.ThrowIfNull(draft);

        var body = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["entity_id"] = todoListEntityId,
            ["item"] = itemUid,
        };
        AddOptionalFields(body, draft, includeRename: true);

        using var response = await PostServiceAsync("todo/update_item", body, returnResponse: false, cancellationToken).ConfigureAwait(false);
    }

    public Task DeleteTodoItemAsync(string todoListEntityId, string itemUid, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(todoListEntityId);
        ArgumentNullException.ThrowIfNull(itemUid);
        return TrackAsync("todo.remove_item", todoListEntityId, HttpMethod.Post.Method, () => DeleteTodoItemCoreAsync(todoListEntityId, itemUid, cancellationToken), cancellationToken);
    }

    private async Task DeleteTodoItemCoreAsync(string todoListEntityId, string itemUid, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(todoListEntityId);
        ArgumentNullException.ThrowIfNull(itemUid);

        var body = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["entity_id"] = todoListEntityId,
            ["item"] = itemUid,
        };

        using var response = await PostServiceAsync("todo/remove_item", body, returnResponse: false, cancellationToken).ConfigureAwait(false);
    }

    public Task<IReadOnlyList<HomeAssistantCalendarEvent>> GetCalendarEventsAsync(
        string calendarEntityId,
        DateOnly fromInclusive,
        DateOnly toExclusive,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(calendarEntityId);
        return TrackAsync("calendar.get_events", calendarEntityId, HttpMethod.Get.Method, () => GetCalendarEventsCoreAsync(calendarEntityId, fromInclusive, toExclusive, cancellationToken), cancellationToken);
    }

    private async Task<IReadOnlyList<HomeAssistantCalendarEvent>> GetCalendarEventsCoreAsync(
        string calendarEntityId,
        DateOnly fromInclusive,
        DateOnly toExclusive,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(calendarEntityId);

        // Pad the range by one day so events crossing the boundary are not clipped away.
        var start = FormatDate(fromInclusive.AddDays(-1));
        var end = FormatDate(toExclusive.AddDays(1));
        var uri = $"api/calendars/{Uri.EscapeDataString(calendarEntityId)}?start={start}T00:00:00Z&end={end}T00:00:00Z";

        using var response = await SendAsync(HttpMethod.Get, uri, content: null, cancellationToken).ConfigureAwait(false);
        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            throw new HomeAssistantRequestException($"Calendar '{calendarEntityId}' returned an unexpected payload.", isTransient: false);
        }

        var events = new List<HomeAssistantCalendarEvent>();
        foreach (var element in document.RootElement.EnumerateArray())
        {
            var calendarEvent = ParseCalendarEvent(element);
            if (calendarEvent is not null)
            {
                events.Add(calendarEvent);
            }
        }

        return events;
    }

    private static void AddOptionalFields(Dictionary<string, object?> body, HomeAssistantTodoItemDraft draft, bool includeRename)
    {
        if (includeRename)
        {
            body["rename"] = draft.Summary;
        }

        if (!string.IsNullOrWhiteSpace(draft.Description))
        {
            body["description"] = draft.Description;
        }

        if (draft.DueDate is DateOnly dueDate)
        {
            body["due_date"] = FormatDate(dueDate);
        }

        body["status"] = draft.Status == HomeAssistantTodoStatus.Completed ? "completed" : "needs_action";
    }

    /// <summary>
    /// Runs one outbound call and reports its outcome. A successful Home Assistant call of the
    /// kind this adapter makes is always a 200, which is what the diagnostic log records.
    /// </summary>
    private async Task<T> TrackAsync<T>(
        string operation,
        string? entityId,
        string method,
        Func<Task<T>> action,
        CancellationToken cancellationToken)
    {
        if (callRecorder is null)
        {
            return await action().ConfigureAwait(false);
        }

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var result = await action().ConfigureAwait(false);
            TryRecord(operation, entityId, method, succeeded: true, statusCode: 200, stopwatch.ElapsedMilliseconds, errorMessage: null, isTransient: null);
            return result;
        }
        catch (HomeAssistantRequestException exception)
        {
            TryRecord(operation, entityId, method, succeeded: false, exception.StatusCode, stopwatch.ElapsedMilliseconds, exception.Message, exception.IsTransient);
            throw;
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested
            && exception is HttpRequestException or TaskCanceledException)
        {
            TryRecord(operation, entityId, method, succeeded: false, statusCode: null, stopwatch.ElapsedMilliseconds, exception.Message, isTransient: true);
            throw;
        }
    }

    private async Task TrackAsync(
        string operation,
        string? entityId,
        string method,
        Func<Task> action,
        CancellationToken cancellationToken)
    {
        if (callRecorder is null)
        {
            await action().ConfigureAwait(false);
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        try
        {
            await action().ConfigureAwait(false);
            TryRecord(operation, entityId, method, succeeded: true, statusCode: 200, stopwatch.ElapsedMilliseconds, errorMessage: null, isTransient: null);
        }
        catch (HomeAssistantRequestException exception)
        {
            TryRecord(operation, entityId, method, succeeded: false, exception.StatusCode, stopwatch.ElapsedMilliseconds, exception.Message, exception.IsTransient);
            throw;
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested
            && exception is HttpRequestException or TaskCanceledException)
        {
            TryRecord(operation, entityId, method, succeeded: false, statusCode: null, stopwatch.ElapsedMilliseconds, exception.Message, isTransient: true);
            throw;
        }
    }

    private void TryRecord(
        string operation,
        string? entityId,
        string method,
        bool succeeded,
        int? statusCode,
        long durationMs,
        string? errorMessage,
        bool? isTransient)
    {
        try
        {
            callRecorder?.Record(new HomeAssistantCallRecord(
                operation,
                entityId,
                method,
                succeeded,
                statusCode,
                durationMs,
                errorMessage,
                isTransient));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Diagnostics are best effort: never let them break the integration call.
            Debug.WriteLine($"Home Assistant call log could not be recorded: {exception.Message}");
        }
    }

    private async Task<HttpResponseMessage> PostServiceAsync(string service, object body, bool returnResponse, CancellationToken cancellationToken)
    {
        var uri = returnResponse ? $"api/services/{service}?return_response" : $"api/services/{service}";
        var json = JsonSerializer.Serialize(body, SerializerOptions);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        return await SendAsync(HttpMethod.Post, uri, content, cancellationToken).ConfigureAwait(false);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string uri, HttpContent? content, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, uri) { Content = content };
        var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var status = response.StatusCode;
            response.Dispose();
            throw new HomeAssistantRequestException(
                $"Home Assistant returned {((int)status).ToString(CultureInfo.InvariantCulture)} for '{uri}'.",
                isTransient: status is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests || (int)status >= 500)
            {
                StatusCode = (int)status,
            };
        }

        return response;
    }

    private static List<HomeAssistantTodoItem> ParseItems(string json, string todoListEntityId)
    {
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("service_response", out var serviceResponse)
            || serviceResponse.ValueKind != JsonValueKind.Object)
        {
            return [];
        }

        var listRoot = serviceResponse.TryGetProperty(todoListEntityId, out var direct)
            ? direct
            : serviceResponse.EnumerateObject().Select(property => property.Value).FirstOrDefault();

        if (listRoot.ValueKind != JsonValueKind.Object
            || !listRoot.TryGetProperty("items", out var itemsElement)
            || itemsElement.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var items = new List<HomeAssistantTodoItem>();
        foreach (var element in itemsElement.EnumerateArray())
        {
            items.Add(ParseTodoItem(element));
        }

        return items;
    }

    private static HomeAssistantTodoItem ParseTodoItem(JsonElement element)
    {
        var uid = GetString(element, "uid") ?? string.Empty;
        var summary = GetString(element, "summary") ?? string.Empty;
        var description = GetString(element, "description");
        var status = string.Equals(GetString(element, "status"), "completed", StringComparison.OrdinalIgnoreCase)
            ? HomeAssistantTodoStatus.Completed
            : HomeAssistantTodoStatus.NeedsAction;

        return new HomeAssistantTodoItem(uid, summary, description, ParseDueDate(element), status);
    }

    private static DateOnly? ParseDueDate(JsonElement element)
    {
        var raw = GetString(element, "due") ?? GetString(element, "due_date");
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var datePart = raw.Length >= 10 ? raw[..10] : raw;
        return DateOnly.TryParse(datePart, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : null;
    }

    private static HomeAssistantCalendarEvent? ParseCalendarEvent(JsonElement element)
    {
        if (!element.TryGetProperty("start", out var start) || !element.TryGetProperty("end", out var end))
        {
            return null;
        }

        var isAllDay = start.TryGetProperty("date", out var startDate) && startDate.ValueKind == JsonValueKind.String;
        var startsAt = ParseInstant(start, isAllDay);
        var endsAt = ParseInstant(end, isAllDay);
        if (startsAt is null || endsAt is null || endsAt <= startsAt)
        {
            return null;
        }

        return new HomeAssistantCalendarEvent(GetString(element, "summary"), startsAt.Value, endsAt.Value, isAllDay);
    }

    private static DateTimeOffset? ParseInstant(JsonElement element, bool isAllDay)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (element.TryGetProperty("dateTime", out var dateTime) && dateTime.ValueKind == JsonValueKind.String)
        {
            return DateTimeOffset.TryParse(dateTime.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var instant)
                ? instant
                : null;
        }

        if (isAllDay && element.TryGetProperty("date", out var date) && date.ValueKind == JsonValueKind.String
            && DateOnly.TryParse(date.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            return new DateTimeOffset(parsed.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        }

        return null;
    }

    private static string? GetString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private static string FormatDate(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}

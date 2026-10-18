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
/// identifiers.
/// </summary>
public sealed class HttpHomeAssistantClient(HttpClient httpClient) : IHomeAssistantClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<HomeAssistantTodoItem>> GetTodoItemsAsync(string todoListEntityId, CancellationToken cancellationToken)
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

    public async Task<HomeAssistantTodoItem> CreateTodoItemAsync(string todoListEntityId, HomeAssistantTodoItemDraft draft, CancellationToken cancellationToken)
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

    public async Task UpdateTodoItemAsync(string todoListEntityId, string itemUid, HomeAssistantTodoItemDraft draft, CancellationToken cancellationToken)
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

    public async Task DeleteTodoItemAsync(string todoListEntityId, string itemUid, CancellationToken cancellationToken)
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

    public async Task<IReadOnlyList<HomeAssistantCalendarEvent>> GetCalendarEventsAsync(
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
                isTransient: status is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests || (int)status >= 500);
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

using System.Net;
using System.Net.Http.Json;
using HomeDaemon.IntegrationTests.Support;

namespace HomeDaemon.IntegrationTests;

/// <summary>
/// The bulk import is administrator-only and idempotent, and it starts one recalculation for
/// the whole batch. Completions carry historical dates but no person.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class ChoreImportHttpTests
{
    private readonly HomeDaemonApiFactory _factory;

    public ChoreImportHttpTests(HomeDaemonApiFactory factory) => _factory = factory;

    [Fact]
    public async Task ImportRequiresTheAdministratorRole()
    {
        using var client = _factory.CreateClient();

        var response = await PostImportAsync(client, NewRequest($"Anonymous {Guid.NewGuid():N}"), authenticated: false);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ImportCreatesTheChoreAndStartsOneRecalculation()
    {
        await _factory.EnsureSeededAsync();
        using var client = await _factory.SignInAsAdminAsync();

        var request = NewRequest($"Imported {Guid.NewGuid():N}");
        var first = await PostImportAsync(client, request, authenticated: true);

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        var created = await first.Content.ReadFromJsonAsync<ChoreImportResponseDto>(TestContext.Current.CancellationToken);
        created!.Imported.Should().Be(1);
        created.Skipped.Should().Be(0);
        created.CompletionsImported.Should().Be(1);
        created.RecalculationRunId.Should().NotBeNull();

        var second = await PostImportAsync(client, request, authenticated: true);
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        var skipped = await second.Content.ReadFromJsonAsync<ChoreImportResponseDto>(TestContext.Current.CancellationToken);
        skipped!.Imported.Should().Be(0);
        skipped.Skipped.Should().Be(1);
        skipped.CompletionsImported.Should().Be(0);
        skipped.CompletionsSkipped.Should().Be(1);
        skipped.RecalculationRunId.Should().BeNull();
    }

    private static ImportRequest NewRequest(string title) =>
        new(
            [
                new
                {
                    line = 2,
                    sourceId = Guid.NewGuid().ToString("N"),
                    title,
                    description = (string?)null,
                    room = (string?)null,
                    estimatedMinutes = 15,
                    isActive = true,
                    effortWeight = (double?)null,
                    preferSameRoomGrouping = false,
                    frequency = "Daily",
                    interval = 1,
                    mode = "Dynamic",
                    anchorDate = (string?)null,
                    assignmentPolicy = "Both",
                    fixedAssigneeMemberId = (Guid?)null,
                    syntheticCompletionDate = "2026-09-01",
                    completions = Array.Empty<string>(),
                },
            ],
            Array.Empty<object>());

    private async Task<HttpResponseMessage> PostImportAsync(HttpClient client, object request, bool authenticated)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "/api/v1/chores/imports")
        {
            Content = JsonContent.Create(request),
        };

        if (authenticated)
        {
            var token = await HomeDaemonApiFactory.GetCsrfTokenAsync(client);
            message.Headers.TryAddWithoutValidation("X-CSRF-TOKEN", token);
        }

        return await client.SendAsync(message, TestContext.Current.CancellationToken);
    }

    private sealed record ImportRequest(
        IReadOnlyList<object> Chores,
        IReadOnlyList<object> Rooms);

    private sealed record ChoreImportResponseDto(
        int Imported,
        int Skipped,
        int CompletionsImported,
        int CompletionsSkipped,
        Guid? RecalculationRunId);
}

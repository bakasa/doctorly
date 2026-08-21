using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Doctorly.Api.Tests;

// exercises the real HTTP pipeline: routing, model binding, ETag/If-Match handling,
// the exception handler's status code mapping. EventPersistenceTests covers the
// repository/EF layer directly, this covers everything above it.
public class EventsEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public EventsEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    private static object CreateEventBody(string title, string search) => new
    {
        title,
        description = $"description for {search}",
        start = DateTimeOffset.UtcNow.AddDays(10).ToString("O"),
        end = DateTimeOffset.UtcNow.AddDays(10).AddMinutes(30).ToString("O"),
        attendees = new[] { new { name = "Jane Doe", email = $"{search}@example.com" } }
    };

    [Fact]
    public async Task Create_Get_Update_Cancel_FullLifecycle()
    {
        var search = $"lifecycle-{Guid.NewGuid():N}";

        var createResponse = await _client.PostAsJsonAsync("/api/v1/events", CreateEventBody("Lifecycle test", search));
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.Equal("\"1\"", createResponse.Headers.ETag!.Tag);

        var created = await createResponse.Content.ReadFromJsonAsync<EventDto>();
        var id = created!.Id;

        var getResponse = await _client.GetAsync($"/api/v1/events/{id}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.Equal("\"1\"", getResponse.Headers.ETag!.Tag);

        var updateNoIfMatch = await _client.PutAsJsonAsync($"/api/v1/events/{id}",
            new { title = "Updated", description = "d", start = DateTimeOffset.UtcNow.AddDays(10).ToString("O"), end = DateTimeOffset.UtcNow.AddDays(10).AddMinutes(45).ToString("O") });
        Assert.Equal(HttpStatusCode.BadRequest, updateNoIfMatch.StatusCode);

        var staleRequest = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/events/{id}")
        {
            Content = JsonContent.Create(new { title = "Updated", description = "d", start = DateTimeOffset.UtcNow.AddDays(10).ToString("O"), end = DateTimeOffset.UtcNow.AddDays(10).AddMinutes(45).ToString("O") })
        };
        staleRequest.Headers.Add("If-Match", "\"999\"");
        var staleResponse = await _client.SendAsync(staleRequest);
        Assert.Equal(HttpStatusCode.PreconditionFailed, staleResponse.StatusCode);

        var correctRequest = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/events/{id}")
        {
            Content = JsonContent.Create(new { title = "Updated", description = "d", start = DateTimeOffset.UtcNow.AddDays(10).ToString("O"), end = DateTimeOffset.UtcNow.AddDays(10).AddMinutes(45).ToString("O") })
        };
        correctRequest.Headers.Add("If-Match", "\"1\"");
        var updateResponse = await _client.SendAsync(correctRequest);
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        Assert.Equal("\"2\"", updateResponse.Headers.ETag!.Tag);

        var cancelResponse = await _client.DeleteAsync($"/api/v1/events/{id}");
        Assert.Equal(HttpStatusCode.NoContent, cancelResponse.StatusCode);

        var afterCancel = await _client.GetFromJsonAsync<EventDto>($"/api/v1/events/{id}");
        Assert.Equal("Cancelled", afterCancel!.Status);
    }

    [Fact]
    public async Task AddAttendee_ThenRespond_UpdatesRsvp()
    {
        var search = $"rsvp-{Guid.NewGuid():N}";
        var create = await _client.PostAsJsonAsync("/api/v1/events", CreateEventBody("RSVP test", search));
        var created = await create.Content.ReadFromJsonAsync<EventDto>();

        var addResponse = await _client.PostAsJsonAsync($"/api/v1/events/{created!.Id}/attendees",
            new { name = "Second Attendee", email = $"{search}-second@example.com" });
        Assert.Equal(HttpStatusCode.Created, addResponse.StatusCode);

        var afterAdd = await addResponse.Content.ReadFromJsonAsync<EventDto>();
        var attendeeId = afterAdd!.Attendees.Single(a => a.Email == $"{search}-second@example.com").Id;

        var respondResponse = await _client.PatchAsJsonAsync(
            $"/api/v1/events/{created.Id}/attendees/{attendeeId}", new { isAttending = true });
        Assert.Equal(HttpStatusCode.OK, respondResponse.StatusCode);

        var updated = await respondResponse.Content.ReadFromJsonAsync<EventDto>();
        Assert.True(updated!.Attendees.Single(a => a.Id == attendeeId).IsAttending);
    }

    [Fact]
    public async Task List_FiltersBySearchAndAttendeeEmail()
    {
        var search = $"filter-{Guid.NewGuid():N}";
        await _client.PostAsJsonAsync("/api/v1/events", CreateEventBody($"Filter test {search}", search));

        var response = await _client.GetFromJsonAsync<EventPageDto>(
            $"/api/v1/events?search={search}&attendeeEmail={search}@example.com");

        Assert.Single(response!.Items);
    }

    [Fact]
    public async Task Get_UnknownId_Returns404()
    {
        var response = await _client.GetAsync($"/api/v1/events/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Create_TitleTooLong_Returns400()
    {
        var body = CreateEventBody(new string('a', 201), "toolong");
        var response = await _client.PostAsJsonAsync("/api/v1/events", body);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private sealed record EventDto(Guid Id, string Status, List<AttendeeDto> Attendees);
    private sealed record AttendeeDto(Guid Id, string Email, bool? IsAttending);
    private sealed record EventPageDto(List<object> Items, int TotalCount);
}

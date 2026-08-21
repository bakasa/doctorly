using Doctorly.Application.Abstractions;
using Doctorly.Application.Events;
using Doctorly.Application.Events.Dtos;
using Doctorly.Domain.Events;

namespace Doctorly.Api.Endpoints;

public static class EventsEndpoints
{
    public static void MapEventsEndpoints(this IEndpointRouteBuilder app)
    {
        var events = app.MapGroup("/api/v1/events").WithTags("Events");

        events.MapPost("/", CreateEvent);
        events.MapGet("/", ListEvents);
        events.MapGet("/{id:guid}", GetEvent);
        events.MapPut("/{id:guid}", UpdateEvent);
        events.MapDelete("/{id:guid}", CancelEvent);
    }

    private static async Task<IResult> CreateEvent(
        CreateEventRequest request, HttpContext httpContext, EventsAppService appService, CancellationToken cancellationToken)
    {
        var result = await appService.CreateEventAsync(request, cancellationToken);
        SetETag(httpContext, result.Version);
        return Results.Created($"/api/v1/events/{result.Id}", result);
    }

    private static async Task<IResult> ListEvents(
        DateTimeOffset? from, DateTimeOffset? to, EventStatus? status, string? attendeeEmail, string? search,
        EventsAppService appService, CancellationToken cancellationToken, int page = 1, int pageSize = 20)
    {
        var filter = new EventFilter(from, to, status, attendeeEmail, search, page, pageSize);
        var result = await appService.ListEventsAsync(filter, cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetEvent(
        Guid id, HttpContext httpContext, EventsAppService appService, CancellationToken cancellationToken)
    {
        var result = await appService.GetEventAsync(id, cancellationToken);
        SetETag(httpContext, result.Version);
        return Results.Ok(result);
    }

    private static async Task<IResult> UpdateEvent(
        Guid id, UpdateEventRequest request, HttpContext httpContext, EventsAppService appService, CancellationToken cancellationToken)
    {
        if (!TryParseIfMatch(httpContext, out var expectedVersion))
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Missing or invalid If-Match header",
                detail: "PUT requires an If-Match header carrying the event's current ETag (from a prior GET).");

        var result = await appService.UpdateEventAsync(id, request, expectedVersion, cancellationToken);
        SetETag(httpContext, result.Version);
        return Results.Ok(result);
    }

    private static async Task<IResult> CancelEvent(Guid id, EventsAppService appService, CancellationToken cancellationToken)
    {
        await appService.CancelEventAsync(id, cancellationToken);
        return Results.NoContent();
    }

    private static bool TryParseIfMatch(HttpContext httpContext, out int version)
    {
        version = 0;
        var header = httpContext.Request.Headers.IfMatch.ToString();
        return !string.IsNullOrWhiteSpace(header) && int.TryParse(header.Trim('"'), out version);
    }

    private static void SetETag(HttpContext httpContext, int version) =>
        httpContext.Response.Headers.ETag = $"\"{version}\"";
}

using Doctorly.Api.Contracts;
using Doctorly.Application.Events;
using Doctorly.Application.Events.Dtos;

namespace Doctorly.Api.Endpoints;

public static class AttendeesEndpoints
{
    public static void MapAttendeesEndpoints(this IEndpointRouteBuilder app)
    {
        var attendees = app.MapGroup("/api/v1/events/{eventId:guid}/attendees").WithTags("Attendees");

        attendees.MapPost("/", AddAttendee);
        attendees.MapPatch("/{attendeeId:guid}", Respond);
    }

    private static async Task<IResult> AddAttendee(
        Guid eventId, CreateAttendeeRequest request, EventsAppService appService, CancellationToken cancellationToken)
    {
        var result = await appService.AddAttendeeAsync(eventId, request, cancellationToken);
        return Results.Created($"/api/v1/events/{eventId}", result);
    }

    private static async Task<IResult> Respond(
        Guid eventId, Guid attendeeId, RespondRequest request, EventsAppService appService, CancellationToken cancellationToken)
    {
        var result = await appService.RespondAsync(eventId, attendeeId, request.IsAttending, cancellationToken);
        return Results.Ok(result);
    }
}

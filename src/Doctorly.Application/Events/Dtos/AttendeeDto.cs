namespace Doctorly.Application.Events.Dtos;

public sealed record AttendeeDto(Guid Id, string Name, string Email, bool? IsAttending);

public sealed record CreateAttendeeRequest(string Name, string Email, bool? IsAttending = null);

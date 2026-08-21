using Doctorly.Domain.DomainEvents;
using Doctorly.Domain.Events;
using Doctorly.Domain.Exceptions;
using Doctorly.Domain.ValueObjects;

namespace Doctorly.Domain.Tests.Events;

public class EventTests
{
    private static TimeRange ValidTimeRange()
    {
        var start = DateTimeOffset.UtcNow.AddDays(1);
        return new TimeRange(start, start.AddMinutes(30));
    }

    private static Event CreateValidEvent(params (string Name, string Email)[] attendees)
    {
        var attendeeArgs = attendees.Select(a => (a.Name, new EmailAddress(a.Email), (bool?)null));
        return Event.Create("Checkup", "Routine checkup", ValidTimeRange(), attendeeArgs);
    }

    [Fact]
    public void Create_ValidInput_RaisesEventCreated()
    {
        var @event = CreateValidEvent(("Jane Doe", "jane@example.com"));

        var raised = Assert.Single(@event.DomainEvents);
        Assert.IsType<EventCreated>(raised);
    }

    [Fact]
    public void Create_ValidInput_SetsInitialVersion()
    {
        var @event = CreateValidEvent();

        Assert.Equal(1, @event.Version);
    }

    [Fact]
    public void Create_TitleTooLong_Throws()
    {
        var title = new string('a', Event.MaxTitleLength + 1);

        Assert.Throws<DomainException>(() =>
            Event.Create(title, "desc", ValidTimeRange(), []));
    }

    [Fact]
    public void Create_DescriptionTooLong_Throws()
    {
        var description = new string('a', Event.MaxDescriptionLength + 1);

        Assert.Throws<DomainException>(() =>
            Event.Create("title", description, ValidTimeRange(), []));
    }

    [Fact]
    public void Update_ValidInput_IncrementsVersionAndRaisesEventUpdated()
    {
        var @event = CreateValidEvent();
        @event.ClearDomainEvents();
        var versionBefore = @event.Version;

        @event.Update("New title", "New description", ValidTimeRange());

        Assert.Equal(versionBefore + 1, @event.Version);
        Assert.IsType<EventUpdated>(Assert.Single(@event.DomainEvents));
    }

    [Fact]
    public void Cancel_ScheduledEvent_SetsStatusAndRaisesEventCancelled()
    {
        var @event = CreateValidEvent();
        @event.ClearDomainEvents();

        @event.Cancel();

        Assert.Equal(EventStatus.Cancelled, @event.Status);
        Assert.IsType<EventCancelled>(Assert.Single(@event.DomainEvents));
    }

    [Fact]
    public void Cancel_AlreadyCancelled_IsIdempotentAndRaisesNoEvent()
    {
        var @event = CreateValidEvent();
        @event.Cancel();
        @event.ClearDomainEvents();

        @event.Cancel();

        Assert.Empty(@event.DomainEvents);
    }

    [Fact]
    public void Update_CancelledEvent_Throws()
    {
        var @event = CreateValidEvent();
        @event.Cancel();

        Assert.Throws<DomainException>(() => @event.Update("t", "d", ValidTimeRange()));
    }

    [Fact]
    public void AddAttendee_ValidInput_IncrementsVersionAndRaisesAttendeeAdded()
    {
        var @event = CreateValidEvent();
        @event.ClearDomainEvents();

        var attendee = @event.AddAttendee("John Smith", new EmailAddress("john@example.com"));

        Assert.Contains(attendee, @event.Attendees);
        Assert.IsType<AttendeeAdded>(Assert.Single(@event.DomainEvents));
    }

    [Fact]
    public void RespondAttendee_KnownAttendee_UpdatesIsAttendingAndRaisesEvent()
    {
        var @event = CreateValidEvent(("Jane Doe", "jane@example.com"));
        var attendeeId = @event.Attendees.Single().Id;
        @event.ClearDomainEvents();

        @event.RespondAttendee(attendeeId, true);

        Assert.True(@event.Attendees.Single().IsAttending);
        Assert.IsType<AttendeeResponded>(Assert.Single(@event.DomainEvents));
    }

    [Fact]
    public void RespondAttendee_UnknownAttendee_Throws()
    {
        var @event = CreateValidEvent();

        Assert.Throws<DomainException>(() => @event.RespondAttendee(Guid.NewGuid(), true));
    }
}

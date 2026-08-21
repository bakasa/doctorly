using Doctorly.Application.Abstractions;
using Doctorly.Application.Events;
using Doctorly.Application.Events.Dtos;
using Doctorly.Application.Exceptions;
using Doctorly.Application.Notifications;
using Doctorly.Domain.DomainEvents;
using Doctorly.Domain.Events;
using Doctorly.Domain.ValueObjects;
using Moq;

namespace Doctorly.Application.Tests;

public class EventsAppServiceTests
{
    private readonly Mock<IEventRepository> _repository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IDomainEventDispatcher> _dispatcher = new();
    private readonly EventsAppService _sut;

    public EventsAppServiceTests()
    {
        _sut = new EventsAppService(_repository.Object, _unitOfWork.Object, _dispatcher.Object);
    }

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
    public async Task CreateEventAsync_ValidRequest_PersistsAndDispatchesEventCreated()
    {
        var request = new CreateEventRequest(
            "Checkup", "Routine checkup",
            DateTimeOffset.UtcNow.AddDays(1), DateTimeOffset.UtcNow.AddDays(1).AddMinutes(30),
            [new CreateAttendeeRequest("Jane Doe", "jane@example.com")]);

        var result = await _sut.CreateEventAsync(request, CancellationToken.None);

        Assert.Equal("Checkup", result.Title);
        _repository.Verify(r => r.AddAsync(It.IsAny<Event>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        _dispatcher.Verify(d => d.DispatchAsync(
            It.Is<IEnumerable<IDomainEvent>>(events => events.OfType<EventCreated>().Any()),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetEventAsync_UnknownId_ThrowsNotFoundException()
    {
        _repository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Event?)null);

        await Assert.ThrowsAsync<NotFoundException>(() => _sut.GetEventAsync(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task UpdateEventAsync_MatchingVersion_Succeeds()
    {
        var @event = CreateValidEvent();
        _repository.Setup(r => r.GetByIdAsync(@event.Id, It.IsAny<CancellationToken>())).ReturnsAsync(@event);

        var request = new UpdateEventRequest("New title", "New description", ValidTimeRange().Start, ValidTimeRange().End);
        var result = await _sut.UpdateEventAsync(@event.Id, request, @event.Version, CancellationToken.None);

        Assert.Equal("New title", result.Title);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateEventAsync_StaleVersion_ThrowsConcurrencyConflictException()
    {
        var @event = CreateValidEvent();
        _repository.Setup(r => r.GetByIdAsync(@event.Id, It.IsAny<CancellationToken>())).ReturnsAsync(@event);

        var request = new UpdateEventRequest("New title", "New description", ValidTimeRange().Start, ValidTimeRange().End);
        var staleVersion = @event.Version - 1;

        await Assert.ThrowsAsync<ConcurrencyConflictException>(() =>
            _sut.UpdateEventAsync(@event.Id, request, staleVersion, CancellationToken.None));

        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CancelEventAsync_KnownEvent_SetsCancelledAndCommits()
    {
        var @event = CreateValidEvent();
        _repository.Setup(r => r.GetByIdAsync(@event.Id, It.IsAny<CancellationToken>())).ReturnsAsync(@event);

        await _sut.CancelEventAsync(@event.Id, CancellationToken.None);

        Assert.Equal(EventStatus.Cancelled, @event.Status);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RespondAsync_KnownAttendee_UpdatesAndDispatchesAttendeeResponded()
    {
        var @event = CreateValidEvent(("Jane Doe", "jane@example.com"));
        var attendeeId = @event.Attendees.Single().Id;
        _repository.Setup(r => r.GetByIdAsync(@event.Id, It.IsAny<CancellationToken>())).ReturnsAsync(@event);

        var result = await _sut.RespondAsync(@event.Id, attendeeId, true, CancellationToken.None);

        Assert.True(result.Attendees.Single().IsAttending);
        _dispatcher.Verify(d => d.DispatchAsync(
            It.Is<IEnumerable<IDomainEvent>>(events => events.OfType<AttendeeResponded>().Any()),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ListEventsAsync_MapsRepositoryPageToDtoPage()
    {
        var events = new List<Event> { CreateValidEvent(), CreateValidEvent() };
        _repository.Setup(r => r.ListAsync(It.IsAny<EventFilter>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EventPage<Event>(events, 2));

        var result = await _sut.ListEventsAsync(new EventFilter(), CancellationToken.None);

        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.Items.Count);
    }
}

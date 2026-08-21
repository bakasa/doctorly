using Doctorly.Application.Abstractions;
using Doctorly.Domain.Events;
using Doctorly.Domain.ValueObjects;
using Doctorly.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Doctorly.Api.Tests;

// exercises EventRepository/UnitOfWork directly against Postgres, below the HTTP layer
public class EventPersistenceTests
{
    private static DoctorlyDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<DoctorlyDbContext>()
            .UseNpgsql("Host=localhost;Port=55432;Database=doctorly;Username=doctorly;Password=doctorly")
            .Options;
        return new DoctorlyDbContext(options);
    }

    [Fact]
    public async Task RoundTrip_CreateListSearchRespond_Works()
    {
        await using var context = CreateContext();
        var repository = new EventRepository(context);
        var unitOfWork = new UnitOfWork(context);

        var start = DateTimeOffset.UtcNow.AddDays(1);
        var @event = Event.Create(
            "Smoke Test Checkup",
            "Verifying repository materialization",
            new TimeRange(start, start.AddMinutes(30)),
            [("Jane Doe", new EmailAddress("jane.smoketest@example.com"), (bool?)null)]);

        await repository.AddAsync(@event, CancellationToken.None);
        await unitOfWork.CommitAsync(CancellationToken.None);

        var fetched = await repository.GetByIdAsync(@event.Id, CancellationToken.None);
        Assert.NotNull(fetched);
        Assert.Single(fetched!.Attendees);
        Assert.Equal("jane.smoketest@example.com", fetched.Attendees.Single().Email.Value);

        var listed = await repository.ListAsync(
            new EventFilter(Search: "Smoke Test", AttendeeEmail: "jane.smoketest@example.com"),
            CancellationToken.None);
        Assert.Contains(listed.Items, e => e.Id == @event.Id);

        var attendeeId = fetched.Attendees.Single().Id;
        fetched.RespondAttendee(attendeeId, true);
        await unitOfWork.CommitAsync(CancellationToken.None);

        var revisionCount = await context.EventRevisions.CountAsync(r => r.EventId == @event.Id);
        Assert.Equal(2, revisionCount);
    }

    [Fact]
    public async Task AddAttendee_ToAlreadyPersistedEvent_InsertsNewRow()
    {
        await using var writeContext = CreateContext();
        var writeRepository = new EventRepository(writeContext);
        var writeUnitOfWork = new UnitOfWork(writeContext);

        var start = DateTimeOffset.UtcNow.AddDays(3);
        var @event = Event.Create("Add attendee regression check", "desc", new TimeRange(start, start.AddMinutes(30)),
            [("First Attendee", new EmailAddress("first@example.com"), (bool?)null)]);
        await writeRepository.AddAsync(@event, CancellationToken.None);
        await writeUnitOfWork.CommitAsync(CancellationToken.None);

        // reload in a fresh context, mirroring a separate HTTP request loading an
        // already-persisted event before adding a new attendee to it
        await using var context = CreateContext();
        var repository = new EventRepository(context);
        var unitOfWork = new UnitOfWork(context);

        var fetched = await repository.GetByIdAsync(@event.Id, CancellationToken.None);
        fetched!.AddAttendee("Second Attendee", new EmailAddress("second@example.com"));
        await unitOfWork.CommitAsync(CancellationToken.None);

        var refetched = await repository.GetByIdAsync(@event.Id, CancellationToken.None);
        Assert.Equal(2, refetched!.Attendees.Count);
        Assert.Contains(refetched.Attendees, a => a.Email.Value == "first@example.com");
        Assert.Contains(refetched.Attendees, a => a.Email.Value == "second@example.com");
    }

    [Fact]
    public async Task Update_StaleVersion_ThrowsConcurrencyException()
    {
        await using var writerContext = CreateContext();
        var repository = new EventRepository(writerContext);
        var unitOfWork = new UnitOfWork(writerContext);

        var start = DateTimeOffset.UtcNow.AddDays(2);
        var @event = Event.Create("Concurrency check", "desc", new TimeRange(start, start.AddMinutes(30)), []);
        await repository.AddAsync(@event, CancellationToken.None);
        await unitOfWork.CommitAsync(CancellationToken.None);

        await using var contextA = CreateContext();
        await using var contextB = CreateContext();
        var eventA = await new EventRepository(contextA).GetByIdAsync(@event.Id, CancellationToken.None);
        var eventB = await new EventRepository(contextB).GetByIdAsync(@event.Id, CancellationToken.None);

        eventA!.Update("Changed by A", "desc", new TimeRange(start, start.AddMinutes(45)));
        await new UnitOfWork(contextA).CommitAsync(CancellationToken.None);

        eventB!.Update("Changed by B", "desc", new TimeRange(start, start.AddMinutes(45)));
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() =>
            new UnitOfWork(contextB).CommitAsync(CancellationToken.None));
    }
}

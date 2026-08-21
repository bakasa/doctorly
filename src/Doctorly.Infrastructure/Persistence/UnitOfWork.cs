using System.Text.Json;
using Doctorly.Application.Abstractions;
using Doctorly.Domain.Events;
using Microsoft.EntityFrameworkCore;

namespace Doctorly.Infrastructure.Persistence;

public sealed class UnitOfWork(DoctorlyDbContext context) : IUnitOfWork
{
    public async Task CommitAsync(CancellationToken cancellationToken)
    {
        var changedEvents = context.ChangeTracker.Entries<Event>()
            .Where(e => e.State is EntityState.Added or EntityState.Modified)
            .Select(e => e.Entity)
            .ToList();

        foreach (var @event in changedEvents)
        {
            context.EventRevisions.Add(new EventRevision
            {
                Id = Guid.NewGuid(),
                EventId = @event.Id,
                Version = @event.Version,
                Snapshot = JsonSerializer.Serialize(ToSnapshot(@event)),
                CreatedAtUtc = DateTimeOffset.UtcNow
            });
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    // deliberately not JsonSerializer.Serialize(event) directly - DomainEvents is an
    // interface-typed collection with no polymorphic type info and would serialize empty
    private static object ToSnapshot(Event @event) => new
    {
        @event.Id,
        @event.Title,
        @event.Description,
        Start = @event.TimeRange.Start,
        End = @event.TimeRange.End,
        @event.Status,
        @event.Version,
        Attendees = @event.Attendees.Select(a => new { a.Id, a.Name, Email = a.Email.Value, a.IsAttending })
    };
}

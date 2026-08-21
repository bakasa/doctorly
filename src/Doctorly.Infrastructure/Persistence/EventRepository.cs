using Doctorly.Application.Abstractions;
using Doctorly.Domain.Events;
using Microsoft.EntityFrameworkCore;

namespace Doctorly.Infrastructure.Persistence;

public sealed class EventRepository(DoctorlyDbContext context) : IEventRepository
{
    public Task<Event?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        context.Events
            .Include(e => e.Attendees)
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    public async Task<EventPage<Event>> ListAsync(EventFilter filter, CancellationToken cancellationToken)
    {
        var query = context.Events.Include(e => e.Attendees).AsQueryable();

        if (filter.Status is { } status)
            query = query.Where(e => e.Status == status);

        // "overlaps the queried window" - matches a calendar view, not a strict containment
        if (filter.From is { } from)
            query = query.Where(e => e.TimeRange.End >= from);

        if (filter.To is { } to)
            query = query.Where(e => e.TimeRange.Start <= to);

        if (!string.IsNullOrWhiteSpace(filter.AttendeeEmail))
            query = query.Where(e => e.Attendees.Any(a => a.Email.Value == filter.AttendeeEmail));

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var pattern = $"%{filter.Search}%";
            query = query.Where(e => EF.Functions.ILike(e.Title, pattern) || EF.Functions.ILike(e.Description, pattern));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(e => e.TimeRange.Start)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(cancellationToken);

        return new EventPage<Event>(items, totalCount);
    }

    public async Task AddAsync(Event @event, CancellationToken cancellationToken) =>
        await context.Events.AddAsync(@event, cancellationToken);
}

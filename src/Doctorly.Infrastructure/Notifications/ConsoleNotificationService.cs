using Doctorly.Application.Abstractions;
using Doctorly.Domain.DomainEvents;
using Microsoft.Extensions.Logging;

namespace Doctorly.Infrastructure.Notifications;

// stands in for real Email/iCal/MQ delivery, see README for the swap-in point
public sealed class ConsoleNotificationService(ILogger<ConsoleNotificationService> logger) : INotificationService
{
    public Task NotifyAsync(IDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        var message = domainEvent switch
        {
            EventCreated e => $"event '{e.Title}' ({e.EventId}) created",
            EventUpdated e => $"event '{e.Title}' ({e.EventId}) updated",
            EventCancelled e => $"event '{e.Title}' ({e.EventId}) cancelled",
            AttendeeAdded e => $"attendee {e.Email} added to event {e.EventId}",
            AttendeeResponded e => $"attendee {e.AttendeeId} on event {e.EventId} responded {(e.IsAttending ? "accepted" : "declined")}",
            _ => $"unhandled domain event {domainEvent.GetType().Name}"
        };

        logger.LogInformation("Notification: {Message}", message);
        return Task.CompletedTask;
    }
}

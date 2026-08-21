using Doctorly.Application.Abstractions;
using Doctorly.Domain.DomainEvents;

namespace Doctorly.Application.Notifications;

public interface IDomainEventDispatcher
{
    Task DispatchAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken cancellationToken);
}

public sealed class DomainEventDispatcher(INotificationService notificationService) : IDomainEventDispatcher
{
    public async Task DispatchAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken cancellationToken)
    {
        foreach (var domainEvent in domainEvents)
            await notificationService.NotifyAsync(domainEvent, cancellationToken);
    }
}

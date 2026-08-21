using Doctorly.Domain.DomainEvents;

namespace Doctorly.Application.Abstractions;

public interface INotificationService
{
    Task NotifyAsync(IDomainEvent domainEvent, CancellationToken cancellationToken);
}

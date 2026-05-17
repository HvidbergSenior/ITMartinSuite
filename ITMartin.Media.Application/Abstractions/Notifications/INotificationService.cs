namespace ITMartin.Media.Application.Abstractions.Notifications;

public interface INotificationService
{
    Task NotifyAsync(
        string message,
        CancellationToken cancellationToken);
}
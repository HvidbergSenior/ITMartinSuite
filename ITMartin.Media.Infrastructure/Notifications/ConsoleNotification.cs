namespace ITMartin.Media.Infrastructure.Notifications;

using ITMartin.Media.Application.Abstractions.Notifications;

public sealed class ConsoleNotificationService
    : INotificationService
{
    public Task NotifyAsync(
        string message,
        CancellationToken cancellationToken)
    {
        Console.WriteLine(message);

        return Task.CompletedTask;
    }
}
namespace ITMartin.Media.Infrastructure.Telemetry;

using ITMartin.Media.Application.Abstractions.Telemetry;

public sealed class ConsoleTelemetryService
    : ITelemetryService
{
    public Task TrackAsync(
        string eventName,
        Dictionary<string, string>? properties,
        CancellationToken cancellationToken)
    {
        Console.WriteLine(
            $"Telemetry: {eventName}");

        return Task.CompletedTask;
    }
}
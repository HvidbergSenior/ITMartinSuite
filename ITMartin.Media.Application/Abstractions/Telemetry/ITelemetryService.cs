namespace ITMartin.Media.Application.Abstractions.Telemetry;

public interface ITelemetryService
{
    Task TrackAsync(
        string eventName,
        Dictionary<string, string>? properties,
        CancellationToken cancellationToken);
}
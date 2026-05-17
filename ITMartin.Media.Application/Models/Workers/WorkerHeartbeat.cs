namespace ITMartin.Media.Application.Models.Workers;

public sealed class WorkerHeartbeat
{
    public string WorkerName { get; set; }
        = string.Empty;

    public string MachineName { get; set; }
        = Environment.MachineName;

    public DateTimeOffset LastSeenAt { get; set; }

    public int ActiveJobs { get; set; }

    public bool IsHealthy { get; set; }
}
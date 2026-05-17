namespace ITMartin.Media.Application.Models.Distributed;

public sealed class WorkerNode
{
    public Guid Id { get; set; }

    public string Name { get; set; }
        = string.Empty;

    public string MachineName { get; set; }
        = Environment.MachineName;

    public DateTimeOffset RegisteredAt { get; set; }

    public DateTimeOffset LastHeartbeat { get; set; }

    public bool IsOnline { get; set; }
}
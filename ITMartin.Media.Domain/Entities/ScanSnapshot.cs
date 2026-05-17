namespace ITMartin.Media.Domain.Entities;

public sealed class ScanSnapshot
{
    public Guid Id { get; set; }

    public Guid SessionId { get; set; }

    public string SerializedState { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }
}
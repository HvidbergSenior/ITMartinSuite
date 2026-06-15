using ITMartin.Magic.Domain.Entities;

public sealed class ScanSession
{
    public Guid Id { get; set; }

    public DateTime CreatedAt { get; set; }

    public Guid? MagicSetId { get; set; }

    public MagicSet? MagicSet { get; set; }

    public ICollection<ScanImage> Images { get; set; } = [];
}
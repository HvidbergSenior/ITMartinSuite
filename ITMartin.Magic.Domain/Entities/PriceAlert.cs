namespace ITMartin.Magic.Domain.Entities;

public sealed class PriceAlert
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string CardName { get; set; } = "";
    public string SetCode { get; set; } = "";
    public decimal OldPrice { get; set; }
    public decimal NewPrice { get; set; }
    public decimal Delta { get; set; }
    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
    public bool Dismissed { get; set; }
}

namespace ITMartinMarket.Domain.Entities;

public sealed class ItemMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SaleItemId { get; set; }
    public string SenderName { get; set; } = "";
    public string Text { get; set; } = "";
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
}

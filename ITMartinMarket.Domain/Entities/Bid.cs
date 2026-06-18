namespace ITMartinMarket.Domain.Entities;

public sealed class Bid
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SaleItemId { get; set; }
    public string BuyerName { get; set; } = "";
    public decimal? Amount { get; set; }
    public DateTime PlacedAt { get; set; } = DateTime.UtcNow;
}

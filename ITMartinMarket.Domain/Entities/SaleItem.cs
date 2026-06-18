namespace ITMartinMarket.Domain.Entities;

public sealed class SaleItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public decimal AskingPrice { get; set; }
    public string? ImagePath { get; set; }
    public string SellerName { get; set; } = "";
    public bool IsSold { get; set; }
    public string? SoldTo { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SoldAt { get; set; }
    public List<Bid> Bids { get; set; } = [];
    public List<ItemMessage> Messages { get; set; } = [];
}

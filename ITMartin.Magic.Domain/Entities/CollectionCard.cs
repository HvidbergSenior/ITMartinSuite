namespace ITMartin.Magic.Domain.Entities;

public class CollectionCard
{
    public Guid Id { get; set; }

    public string Name { get; set; } = "";

    public string SetCode { get; set; } = "";

    public int Quantity { get; set; }

    public string ImageUrl { get; set; } = "";

    public DateTime AddedAt { get; set; }
}
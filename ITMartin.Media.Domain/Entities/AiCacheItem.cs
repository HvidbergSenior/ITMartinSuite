namespace ITMartin.Media.Domain.Entities;

public sealed class AiCacheItem
{
    public string Hash { get; set; } = "";

    public string Description { get; set; } = "";

    public List<string> Tags { get; set; } = [];

    public double Confidence { get; set; }

    public DateTime CreatedAt { get; set; }
}
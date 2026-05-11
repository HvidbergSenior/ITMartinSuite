namespace ITMartin.Media.Infrastructure.Entities;

public sealed class AiCache
{
    public string Hash { get; set; } = "";

    public string Description { get; set; } = "";

    public string TagsJson { get; set; } = "";

    public double Confidence { get; set; }

    public DateTime CreatedAt { get; set; }
}
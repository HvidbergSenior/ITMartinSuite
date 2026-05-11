namespace ITMartin.Media.Models;

public sealed class AiBatchResult
{
    public Guid Id { get; set; }

    public string Category { get; set; } = "";

    public string SubCategory { get; set; } = "";

    public string Description { get; set; } = "";

    public double Confidence { get; set; }
}
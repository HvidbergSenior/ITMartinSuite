namespace ITMartin.Media.Models;

public sealed class AiResult
{
    public string? Category { get; set; }
    public string? SubCategory { get; set; }
    public string? Description { get; set; }
    public float Confidence { get; set; }
}
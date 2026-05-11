namespace ITMartin.Media.Domain.Models;

public class AiAnalysisResult
{
    public string Description { get; set; } = string.Empty;

    public List<string> Tags { get; set; } = [];

    public double Confidence { get; set; }
    public string FullPath { get; set; } = "";
}
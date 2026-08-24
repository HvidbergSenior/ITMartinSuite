namespace ITMartin.Media.Contracts.Contracts.Runtime.Models;

public class AiAnalysisResult
{
    public string Description { get; set; } = string.Empty;

    public List<string> Tags { get; set; } = [];

    public double Confidence { get; set; }

    public string FullPath { get; set; } = "";

    public bool IsBlurry { get; set; }

    public bool IsSolidColor { get; set; }

    public bool IsMeme { get; set; }

    public bool IsScreenshot { get; set; }

    public bool IsChat { get; set; }
}
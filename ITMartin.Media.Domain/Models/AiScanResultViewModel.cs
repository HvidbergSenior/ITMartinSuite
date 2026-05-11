namespace ITMartin.Media.Domain.Models;

public sealed class AiScanResultViewModel
{
    public string FullPath { get; set; } = "";

    public string FileName { get; set; } = "";

    public string? OcrText { get; set; }

    public string? AiDescription { get; set; }

    public List<string> AiTags { get; set; } = [];

    public double AiConfidence { get; set; }
}
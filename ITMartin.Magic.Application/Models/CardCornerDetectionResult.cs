namespace ITMartin.Magic.Application.Models;

public class CardCornerDetectionResult
{
    public bool Success { get; set; }

    public CardPoint TopLeft { get; set; } = new();

    public CardPoint TopRight { get; set; } = new();

    public CardPoint BottomRight { get; set; } = new();

    public CardPoint BottomLeft { get; set; } = new();

    public string? DebugImagePath { get; set; }
}
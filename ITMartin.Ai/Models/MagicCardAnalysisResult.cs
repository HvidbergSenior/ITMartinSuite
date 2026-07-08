namespace ITMartin.Ai.Models;

public sealed class MagicCardAnalysisResult
{
    public string? IdentifiedName { get; set; }

    public string? CollectorNumber { get; set; }

    public string? Artist { get; set; }

    public string? ManaCost { get; set; }

    public string? CardType { get; set; }

    public string? PowerToughness { get; set; }

    public string? BorderColor { get; set; }

    public string? CopyrightYear { get; set; }

    // Revised Edition has nothing printed directly under the artist credit
    // line; any later edition (4th Edition onward) has something there.
    // Distinguishes Revised from 4th Edition even when the tiny copyright
    // year text isn't legible in the photo.
    public bool? HasLineUnderArtist { get; set; }

    // Is there an actual expansion symbol printed on the card (usually a small icon
    // to the right of the card name, or near the type line)? Used to catch mode
    // mismatches - user selected "no set symbol" for a card that actually has one.
    public bool? HasVisibleSetSymbol { get; set; }

    public decimal IdentificationConfidence { get; set; }
}
using System.Text.Json.Serialization;

namespace ITMartin.Media.Domain.Models;

public class MagicCardAnalysisResult
{
// =====================================
// CORE
// =====================================

[JsonPropertyName("name")]
public string CardName { get; set; } = "";

// GPT DRIFT FIX
[JsonPropertyName("title")]
public string? LegacyTitle
{
    get => CardName;
    set
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            CardName = value;
        }
    }
}

[JsonPropertyName("confidence")]
public decimal Confidence { get; set; }

[JsonPropertyName("exactPrintingCertain")]
public bool ExactPrintingCertain { get; set; }

// =====================================
// PRINT IDENTIFICATION
// =====================================

[JsonPropertyName("setCode")]
public string? SetCode { get; set; }

[JsonPropertyName("collectorNumber")]
public string? CollectorNumber { get; set; }

[JsonPropertyName("copyright")]
public string? Copyright { get; set; }

[JsonPropertyName("copyrightLine")]
public string? LegacyCopyright
{
    get => Copyright;
    set
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            Copyright = value;
        }
    }
}

[JsonPropertyName("releaseEra")]
public string? ReleaseEra { get; set; }

// =====================================
// VISUAL FRAME ANALYSIS
// =====================================

[JsonPropertyName("oldBorder")]
public bool IsOldBorder { get; set; }

[JsonPropertyName("whiteBorder")]
public bool IsWhiteBorder { get; set; }

// =====================================
// CARD CONTENT
// =====================================

[JsonPropertyName("artist")]
public string? Artist { get; set; }

[JsonPropertyName("manaCost")]
public string? ManaCost { get; set; }

[JsonPropertyName("cardType")]
public string? CardType { get; set; }

[JsonPropertyName("rarity")]
public string? Rarity { get; set; }

[JsonPropertyName("powerToughness")]
public string? PowerToughness { get; set; }

// GPT DRIFT FIX
[JsonPropertyName("power")]
public string? Power { get; set; }

[JsonPropertyName("toughness")]
public string? Toughness { get; set; }

}

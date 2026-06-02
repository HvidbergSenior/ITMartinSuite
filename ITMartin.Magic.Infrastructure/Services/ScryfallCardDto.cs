using System.Text.Json.Serialization;

namespace ITMartin.Magic.Infrastructure.Services;

internal sealed class ScryfallCardDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("set")]
    public string Set { get; set; } = "";

    [JsonPropertyName("collector_number")]
    public string CollectorNumber { get; set; } = "";

    [JsonPropertyName("mana_cost")]
    public string ManaCost { get; set; } = "";

    [JsonPropertyName("type_line")]
    public string TypeLine { get; set; } = "";

    [JsonPropertyName("rarity")]
    public string Rarity { get; set; } = "";

    [JsonPropertyName("oracle_text")]
    public string OracleText { get; set; } = "";

    [JsonPropertyName("artist")]
    public string Artist { get; set; } = "";

    [JsonPropertyName("frame")]
    public string Frame { get; set; } = "";

    [JsonPropertyName("border_color")]
    public string BorderColor { get; set; } = "";

    [JsonPropertyName("power")]
    public string Power { get; set; } = "";

    [JsonPropertyName("toughness")]
    public string Toughness { get; set; } = "";

    [JsonPropertyName("released_at")]
    public string ReleasedAt { get; set; } = "";

    [JsonPropertyName("finishes")]
    public List<string>? Finishes { get; set; }

    [JsonPropertyName("image_uris")]
    public ImageUrisDto? ImageUris { get; set; }

    [JsonPropertyName("prices")]
    public PricesDto? Prices { get; set; }
}

internal sealed class ImageUrisDto
{
    [JsonPropertyName("normal")]
    public string? Normal { get; set; }
}

internal sealed class PricesDto
{
    [JsonPropertyName("eur")]
    public string? Eur { get; set; }

    [JsonPropertyName("eur_foil")]
    public string? EurFoil { get; set; }

    [JsonPropertyName("usd")]
    public string? Usd { get; set; }

    [JsonPropertyName("usd_foil")]
    public string? UsdFoil { get; set; }
}
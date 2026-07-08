using System.Text.Json.Serialization;

namespace ITMartin.Magic.Application.Models;

public sealed class ScryfallSetDto
{
    public string Code { get; set; } = "";

    public string Name { get; set; } = "";

    [JsonPropertyName("released_at")]
    public DateTime ReleasedAt { get; set; }

    [JsonPropertyName("set_type")]
    public string SetType { get; set; } = "";

    [JsonPropertyName("icon_svg_uri")]
    public string IconSvgUri { get; set; } = "";

    [JsonPropertyName("digital")]
    public bool Digital { get; set; }
}
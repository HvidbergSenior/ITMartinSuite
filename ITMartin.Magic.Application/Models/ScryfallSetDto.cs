using System.Text.Json.Serialization;

namespace ITMartin.Magic.Application.Models;

public sealed class ScryfallSetDto
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("released_at")]
    public DateTime ReleasedAt { get; set; }
}
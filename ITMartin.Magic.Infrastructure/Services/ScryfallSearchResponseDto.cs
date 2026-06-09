using System.Text.Json.Serialization;

namespace ITMartin.Magic.Infrastructure.Services;

internal sealed class ScryfallSearchResponse
{
    [JsonPropertyName("data")]
    public List<ScryfallCardDto> Data { get; set; } = [];
}
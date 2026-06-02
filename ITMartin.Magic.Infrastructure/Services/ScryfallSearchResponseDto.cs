using System.Text.Json.Serialization;

namespace ITMartin.Magic.Infrastructure.Services;

internal sealed class ScryfallSearchResponseDto
{
    [JsonPropertyName("data")]
    public List<ScryfallCardDto> Data { get; set; } = [];
}
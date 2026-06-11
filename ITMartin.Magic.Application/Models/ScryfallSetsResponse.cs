using System.Text.Json.Serialization;

namespace ITMartin.Magic.Application.Models;

public sealed class ScryfallSetsResponse
{
    [JsonPropertyName("data")]
    public List<ScryfallSetDto> Data { get; set; } = [];
}
using System.Text.Json.Serialization;

namespace ITMartinLibrary.Infrastructure.ExternalApis;

public class OpenLibraryResponse
{
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("authors")]
    public List<OpenLibraryAuthor>? Authors { get; set; }
}

public class OpenLibraryAuthor
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }
}
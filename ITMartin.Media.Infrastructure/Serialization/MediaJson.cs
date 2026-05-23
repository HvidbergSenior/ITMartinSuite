using System.Text.Json;

namespace ITMartin.Media.Infrastructure.Serialization;

public static class MediaJson
{
    public static readonly JsonSerializerOptions Default =
        new()
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };
}
using ITMartin.Media.Application.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Enums;

namespace ITMartin.Media.Infrastructure.Media;

public sealed class MediaTypeResolver
    : IMediaTypeResolver
{
    public MediaType Resolve(
        string path)
    {
        var extension =
            Path.GetExtension(path)
                .ToLowerInvariant();

        return extension switch
        {
            ".jpg" or ".jpeg" or ".png" or ".gif"
                => MediaType.Image,

            ".mp4" or ".mov" or ".avi"
                => MediaType.Video,

            ".mp3" or ".wav"
                => MediaType.Audio,

            ".pdf" or ".docx"
                => MediaType.Document,

            _ => MediaType.Image
        };
    }
}
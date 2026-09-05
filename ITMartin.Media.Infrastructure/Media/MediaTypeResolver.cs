using ITMartin.Media.Application.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Enums;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;

namespace ITMartin.Media.Infrastructure.Media;

public sealed class MediaTypeResolver
    : IMediaTypeResolver
{
    // 3GP is a container, not a codec guarantee - old phones used it for both
    // real video-with-audio recordings AND audio-only voice/music tracks
    // (no video stream at all). Confirmed 2026-09-03 on Rico/AC's archive:
    // 13 files named "Track NN.3gp" were audio-only and had been landing in
    // Videoer as if they were real videos, purely on extension. Extension
    // alone can't tell the two apart, so this is the one MediaType this
    // resolver can't answer from the extension - it has to actually probe.
    private static readonly HashSet<string> AmbiguousVideoExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".3gp", ".3g2" };

    private readonly IVideoMetadataService _videoMetadataService;

    public MediaTypeResolver(IVideoMetadataService videoMetadataService)
    {
        _videoMetadataService = videoMetadataService;
    }

    public MediaType Resolve(
        string path)
    {
        var type = MediaTypeHelper.GetMediaType(path);

        if (type == MediaType.Video && AmbiguousVideoExtensions.Contains(Path.GetExtension(path)))
        {
            var hasVideoStream = _videoMetadataService.GetVideoCodec(path) is not null;
            if (!hasVideoStream) return MediaType.Audio;
        }

        return type;
    }
}
using ITMartin.Media.Contracts.Contracts.Constants;

namespace ITMartin.Media.Contracts.Contracts.Runtime.Helpers;

public static class MediaFileHelper
{
    public static bool IsSupportedMedia(
        string path)
    {
        var extension =
            Path.GetExtension(path)
                .ToLowerInvariant();

        return
            MediaExtensions.ImageExtensions.Contains(extension)
            || MediaExtensions.VideoExtensions.Contains(extension)
            || MediaExtensions.DocumentExtensions.Contains(extension)
            || MediaExtensions.AudioExtensions.Contains(extension);
    }
}
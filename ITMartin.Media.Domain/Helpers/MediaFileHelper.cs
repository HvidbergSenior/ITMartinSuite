namespace ITMartin.Media.Domain.Helpers;

using ITMartin.Media.Domain.Constants;

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
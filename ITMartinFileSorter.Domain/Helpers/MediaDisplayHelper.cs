namespace ITMartinFileSorter.Domain.Helpers;

public static class MediaDisplayHelper
{
    public static bool IsDisplayable(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();

        return ext is
            // images
            ".jpg" or ".jpeg" or ".png" or ".webp" or ".gif" or

            // video
            ".mp4" or ".mov" or ".mkv" or

            // audio
            ".mp3" or ".wav" or

            // docs
            ".pdf";
    }
}
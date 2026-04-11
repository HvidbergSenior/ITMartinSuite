using ITMartinFileSorter.Domain.Entities;

namespace ITMartinFileSorter.Application.Services;

public class JunkDetectionService
{
    public bool IsScreenshot(MediaFile file)
    {
        var name = file.FileName.ToLower();

        return name.Contains("screenshot")
               || name.Contains("skærmbillede")
               || name.Contains("screen");
    }

    public bool IsLikelyMeme(MediaFile file)
    {
        var ext = file.Extension.ToLower();

        return ext == ".gif" || file.SizeBytes < 50_000;
    }
}
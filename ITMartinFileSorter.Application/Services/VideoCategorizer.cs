using ITMartinFileSorter.Domain.Entities;
using ITMartinFileSorter.Domain.Enums;
using ITMartinFileSorter.Domain.Interfaces;

namespace ITMartinFileSorter.Application.Services;

public class VideoCategorizer : IMediaSubCategorizer
{
    public MediaType Type => MediaType.Video;
    public void Categorize(MediaFile file)
    {
        var name = file.FileName.ToLowerInvariant();

        string yearMonth = file.Year > 1990
            ? $"{file.Year}-{file.Month:00}"
            : "Unknown";

        if (IsScreenRecording(file, name))
        {
            file.SubCategory = MediaSubCategory.ScreenRecording;
        }
        else if (IsPhoneVideo(name))
        {
            file.SubCategory = MediaSubCategory.PhoneVideo;
        }
        else
        {
            file.SubCategory = MediaSubCategory.OtherVideo;
        }
    }

    private bool IsScreenRecording(MediaFile file, string name)
    {
        // filename based
        if (name.Contains("screen") ||
            name.Contains("capture") ||
            name.Contains("recording"))
            return true;

        // resolution based
        if (file.Width.HasValue &&
            file.Height.HasValue &&
            file.Height.Value > file.Width.Value * 1.6)
            return true;

        return false;
    }

    private bool IsPhoneVideo(string name)
    {
        return name.StartsWith("vid_")
               || name.StartsWith("mov_")
               || name.StartsWith("img_")
               || name.StartsWith("pxl_");
    }
}
using ITMartinFileSorter.Domain.Entities;
using ITMartinFileSorter.Domain.Enums;

namespace ITMartinFileSorter.Application.Services;

public class AudioCategorizer
{
    public void Categorize(MediaFile file)
    {
        var ext = file.Extension.ToLowerInvariant();

        string yearOnly = file.Year > 1990
            ? file.Year.ToString()
            : "Unknown";

        if (ext is ".mp3" or ".flac")
            file.SubCategory = MediaSubCategory.Music;

        else if (ext is ".wav" or ".aac")
            file.SubCategory = MediaSubCategory.VoiceMemo;

        else
            file.SubCategory = MediaSubCategory.UnknownAudio;

        file.DynamicFolder = Path.Combine(
            "Music",
            yearOnly
        );
    }
}
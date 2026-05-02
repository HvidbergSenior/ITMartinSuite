using ITMartinFileSorter.Domain.Enums;
using ITMartinFileSorter.Domain.Interfaces;

namespace ITMartinFileSorter.Application.Services;

public class AudioCategorizer : IMediaSubCategorizer
{
    public MediaType Type => MediaType.Audio;
    public void Categorize(MediaFile file)
    {
        if (file == null)
            throw new ArgumentNullException(nameof(file));

        if (file.Type != MediaType.Audio)
            throw new InvalidOperationException(
                $"Invalid media type: {file.Type}. Expected Audio.");

        var ext = file.Extension.ToLowerInvariant();

        file.SubCategory = ext switch
        {
            ".mp3" or ".flac" => MediaSubCategory.Music,
            ".wav" or ".aac" => MediaSubCategory.VoiceMemo,
            _ => MediaSubCategory.UnknownAudio
        };
    }
}
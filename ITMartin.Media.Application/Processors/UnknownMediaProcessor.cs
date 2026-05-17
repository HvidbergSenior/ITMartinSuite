using ITMartin.Media.Domain.Entities;
using ITMartin.Media.Enums;

namespace ITMartin.Media.Application.Processors;

public class UnknownMediaProcessor
{
    public List<MediaFile> Process(
        IEnumerable<MediaFile> files)
    {
        return files
            .Where(f =>
                f.SubCategory ==
                MediaSubCategory.UnknownImage
                ||
                f.SubCategory ==
                MediaSubCategory.UnknownVideo
                ||
                f.SubCategory ==
                MediaSubCategory.UnknownAudio
                ||
                f.SubCategory ==
                MediaSubCategory.UnknownDocument)
            .ToList();
    }
}
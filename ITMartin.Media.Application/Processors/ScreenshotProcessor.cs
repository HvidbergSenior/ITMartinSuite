using ITMartin.Media.Domain.Entities;
using ITMartin.Media.Enums;

namespace ITMartin.Media.Application.Processors;

public class ScreenshotProcessor
{
    public List<MediaFile> Process(
        IEnumerable<MediaFile> files)
    {
        return files
            .Where(f =>
                f.SubCategory ==
                MediaSubCategory.Screenshot)
            .ToList();
    }
}
using ITMartin.Media.Domain.Entities;
using ITMartin.Media.Helpers;

namespace ITMartin.Media.Application.Processors;

public class MediaCategoryProcessor
{
    public Dictionary<string, List<MediaFile>>
        Group(
            IEnumerable<MediaFile> files)
    {
        return files
            .GroupBy(f =>
                CategoryHelper
                    .GetCategory(f))
            .ToDictionary(
                g => g.Key,
                g => g.ToList());
    }
}
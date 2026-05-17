using ITMartin.Media.Domain.Entities;

namespace ITMartin.Media.Application.Processors;

public class ReviewProcessor
{
    public List<MediaFile> Process(
        IEnumerable<MediaFile> files)
    {
        return files
            .Where(f =>
                f.RequiresReview)
            .ToList();
    }
}
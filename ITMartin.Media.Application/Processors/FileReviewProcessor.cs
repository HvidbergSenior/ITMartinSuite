using ITMartin.Media.Domain.Entities;

namespace ITMartin.Media.Application.Processors;

public class FileReviewProcessor
{
    public List<MediaFile> Pending(
        IEnumerable<MediaFile> files)
    {
        return files
            .Where(f =>
                f.RequiresReview)
            .ToList();
    }

    public List<MediaFile> Approved(
        IEnumerable<MediaFile> files)
    {
        return files
            .Where(f =>
                !f.RequiresReview)
            .ToList();
    }
}
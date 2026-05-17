using ITMartin.Media.Domain.Entities;

namespace ITMartin.Media.Application.Processors;

public class OldFileProcessor
{
    public List<MediaFile> Process(
        IEnumerable<MediaFile> files,
        int olderThanDays)
    {
        var max =
            DateTime.UtcNow
                .AddDays(-olderThanDays);

        return files
            .Where(f =>
                f.CreatedAt != null
                &&
                f.CreatedAt <= max)
            .ToList();
    }
}
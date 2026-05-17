using ITMartin.Media.Domain.Entities;

namespace ITMartin.Media.Application.Processors;

public class BestFileProcessor
{
    public MediaFile Select(
        IEnumerable<MediaFile> files)
    {
        return files
            .OrderByDescending(f =>
                f.IsDateReliable)

            .ThenByDescending(f =>
                f.CreatedAt ??
                DateTime.MinValue)

            .ThenByDescending(f =>
                f.SizeBytes)

            .ThenBy(f =>
                f.FullPath)

            .First();
    }
}
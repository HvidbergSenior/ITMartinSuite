using ITMartin.Media.Domain.Entities;

namespace ITMartin.Media.Application.Processors;

public class LargeFileProcessor
{
    public List<MediaFile> Process(
        IEnumerable<MediaFile> files,
        long minBytes)
    {
        return files
            .Where(f =>
                f.SizeBytes >=
                minBytes)
            .ToList();
    }
}
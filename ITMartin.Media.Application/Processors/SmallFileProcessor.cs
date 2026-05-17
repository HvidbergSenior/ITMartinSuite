using ITMartin.Media.Domain.Entities;

namespace ITMartin.Media.Application.Processors;

public class SmallFileProcessor
{
    public List<MediaFile> Process(
        IEnumerable<MediaFile> files,
        long maxBytes)
    {
        return files
            .Where(f =>
                f.SizeBytes <=
                maxBytes)
            .ToList();
    }
}
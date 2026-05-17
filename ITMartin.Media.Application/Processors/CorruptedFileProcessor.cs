using ITMartin.Media.Domain.Entities;

namespace ITMartin.Media.Application.Processors;

public class CorruptedFileProcessor
{
    public List<MediaFile> Process(
        IEnumerable<MediaFile> files)
    {
        return files
            .Where(f =>
                f.SizeBytes <= 0)
            .ToList();
    }
}
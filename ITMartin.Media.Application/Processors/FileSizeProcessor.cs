using ITMartin.Media.Domain.Entities;

namespace ITMartin.Media.Application.Processors;

public class FileSizeProcessor
{
    public long Calculate(
        IEnumerable<MediaFile> files)
    {
        return files.Sum(f =>
            f.SizeBytes);
    }
}
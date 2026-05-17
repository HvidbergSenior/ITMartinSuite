using ITMartin.Media.Domain.Entities;

namespace ITMartin.Media.Application.Processors;

public class MediaFileCountProcessor
{
    public int Count(
        IEnumerable<MediaFile> files)
    {
        return files.Count();
    }
}
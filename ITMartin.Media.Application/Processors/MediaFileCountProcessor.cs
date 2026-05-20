using ITMartin.Media.Contracts.Contracts.Runtime.Models;

namespace ITMartin.Media.Application.Processors;

public class MediaFileCountProcessor
{
    public int Count(
        IEnumerable<MediaFile> files)
    {
        return files.Count();
    }
}
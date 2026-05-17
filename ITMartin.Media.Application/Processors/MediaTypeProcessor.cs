using ITMartin.Media.Domain.Entities;
using ITMartin.Media.Enums;

namespace ITMartin.Media.Application.Processors;

public class MediaTypeProcessor
{
    public List<MediaFile> Images(
        IEnumerable<MediaFile> files)
    {
        return files
            .Where(f =>
                f.Type ==
                MediaType.Image)
            .ToList();
    }

    public List<MediaFile> Videos(
        IEnumerable<MediaFile> files)
    {
        return files
            .Where(f =>
                f.Type ==
                MediaType.Video)
            .ToList();
    }

    public List<MediaFile> Audio(
        IEnumerable<MediaFile> files)
    {
        return files
            .Where(f =>
                f.Type ==
                MediaType.Audio)
            .ToList();
    }

    public List<MediaFile> Documents(
        IEnumerable<MediaFile> files)
    {
        return files
            .Where(f =>
                f.Type ==
                MediaType.Document)
            .ToList();
    }
}
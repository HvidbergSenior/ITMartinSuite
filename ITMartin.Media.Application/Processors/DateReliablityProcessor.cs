using ITMartin.Media.Domain.Entities;

namespace ITMartin.Media.Application.Processors;

public class DateReliabilityProcessor
{
    public List<MediaFile> Reliable(
        IEnumerable<MediaFile> files)
    {
        return files
            .Where(f =>
                f.IsDateReliable)
            .ToList();
    }

    public List<MediaFile> Unreliable(
        IEnumerable<MediaFile> files)
    {
        return files
            .Where(f =>
                !f.IsDateReliable)
            .ToList();
    }
}
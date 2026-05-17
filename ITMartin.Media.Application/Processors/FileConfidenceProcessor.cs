using ITMartin.Media.Domain.Entities;

namespace ITMartin.Media.Application.Processors;

public class FileConfidenceProcessor
{
    public float Get(
        MediaFile file)
    {
        return file.AiConfidence ?? 0;
    }

    public List<MediaFile> HighConfidence(
        IEnumerable<MediaFile> files,
        float min)
    {
        return files
            .Where(f =>
                (f.AiConfidence ?? 0)
                >= min)
            .ToList();
    }

    public List<MediaFile> LowConfidence(
        IEnumerable<MediaFile> files,
        float max)
    {
        return files
            .Where(f =>
                (f.AiConfidence ?? 0)
                <= max)
            .ToList();
    }
}
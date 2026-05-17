using ITMartin.Media.Domain.Entities;

namespace ITMartin.Media.Application.Processors;

public class FileAiProcessor
{
    public List<MediaFile> Processed(
        IEnumerable<MediaFile> files)
    {
        return files
            .Where(f =>
                f.AiProcessed)
            .ToList();
    }

    public List<MediaFile> Unprocessed(
        IEnumerable<MediaFile> files)
    {
        return files
            .Where(f =>
                !f.AiProcessed)
            .ToList();
    }
}
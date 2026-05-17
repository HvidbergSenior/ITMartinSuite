using ITMartin.Media.Domain.Entities;

namespace ITMartin.Media.Application.Processors;

public class FileOcrProcessor
{
    public List<MediaFile> Processed(
        IEnumerable<MediaFile> files)
    {
        return files
            .Where(f =>
                f.OcrProcessed)
            .ToList();
    }

    public List<MediaFile> Unprocessed(
        IEnumerable<MediaFile> files)
    {
        return files
            .Where(f =>
                !f.OcrProcessed)
            .ToList();
    }
}
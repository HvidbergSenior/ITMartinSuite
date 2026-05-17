using ITMartin.Media.Domain.Entities;

namespace ITMartin.Media.Application.Processors;

public class FileExportProcessor
{
    public List<MediaFile> Ready(
        IEnumerable<MediaFile> files)
    {
        return files
            .Where(f =>
                !string.IsNullOrWhiteSpace(
                    f.ExportedPath))
            .ToList();
    }
}
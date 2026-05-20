using ITMartin.Media.Contracts.Contracts.Runtime.Models;

namespace ITMartin.Media.Application.Processors;

public class FileSortingProcessor
{
    public List<MediaFile> ByDate(
        IEnumerable<MediaFile> files)
    {
        return files
            .OrderBy(f =>
                f.CreatedAt)
            .ToList();
    }

    public List<MediaFile> BySizeDescending(
        IEnumerable<MediaFile> files)
    {
        return files
            .OrderByDescending(f =>
                f.SizeBytes)
            .ToList();
    }

    public List<MediaFile> ByName(
        IEnumerable<MediaFile> files)
    {
        return files
            .OrderBy(f =>
                f.FileName)
            .ToList();
    }
}
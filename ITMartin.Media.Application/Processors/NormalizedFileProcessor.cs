using ITMartin.Media.Domain.Entities;

namespace ITMartin.Media.Application.Processors;

public class NormalizedFileProcessor
{
    public bool HasNormalizedPath(
        MediaFile file)
    {
        return !string.IsNullOrWhiteSpace(
            file.NormalizedPath);
    }

    public string? GetPath(
        MediaFile file)
    {
        return file.NormalizedPath;
    }

    public void SetPath(
        MediaFile file,
        string path)
    {
        file.NormalizedPath =
            path;
    }
}
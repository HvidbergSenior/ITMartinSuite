using ITMartin.Media.Domain.Entities;

namespace ITMartin.Media.Application.Processors;

public class ExportedFileProcessor
{
    public bool HasExportPath(
        MediaFile file)
    {
        return !string.IsNullOrWhiteSpace(
            file.ExportedPath);
    }

    public string? GetPath(
        MediaFile file)
    {
        return file.ExportedPath;
    }

    public void SetPath(
        MediaFile file,
        string path)
    {
        file.ExportedPath =
            path;
    }
}
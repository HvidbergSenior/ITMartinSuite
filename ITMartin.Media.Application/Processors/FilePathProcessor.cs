using ITMartin.Media.Domain.Entities;

namespace ITMartin.Media.Application.Processors;

public class FilePathProcessor
{
    public string GetDirectory(
        MediaFile file)
    {
        return Path.GetDirectoryName(
                   file.FullPath)
               ?? "";
    }

    public string GetFileName(
        MediaFile file)
    {
        return Path.GetFileName(
            file.FullPath);
    }

    public string Combine(
        params string[] paths)
    {
        return Path.Combine(paths);
    }
}
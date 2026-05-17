using ITMartin.Media.Domain.Entities;

namespace ITMartin.Media.Application.Processors;

public class FileRenameProcessor
{
    public string Rename(
        MediaFile file,
        string newName)
    {
        var directory =
            Path.GetDirectoryName(
                file.FullPath)!;

        var newPath =
            Path.Combine(
                directory,
                newName);

        File.Move(
            file.FullPath,
            newPath);

        return newPath;
    }
}
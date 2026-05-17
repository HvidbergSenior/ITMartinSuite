using ITMartin.Media.Domain.Entities;

namespace ITMartin.Media.Application.Processors;

public class FileMoveProcessor
{
    public void Move(
        MediaFile file,
        string destination)
    {
        Directory.CreateDirectory(
            Path.GetDirectoryName(
                destination)!);

        File.Move(
            file.FullPath,
            destination,
            true);

        file.ExportedPath =
            destination;
    }
}
using ITMartin.Media.Contracts.Contracts.Runtime.Models;

namespace ITMartin.Media.Application.Processors;

public class FileCopyProcessor
{
    public void Copy(
        MediaFile file,
        string destination)
    {
        Directory.CreateDirectory(
            Path.GetDirectoryName(
                destination)!);

        File.Copy(
            file.FullPath,
            destination,
            true);

        file.ExportedPath =
            destination;
    }
}
using System.IO.Compression;

namespace ITMartin.Media.Application.Processors;

public class FileExtractionProcessor
{
    public void Extract(
        string zipPath,
        string destination)
    {
        Directory.CreateDirectory(
            destination);

        ZipFile.ExtractToDirectory(
            zipPath,
            destination,
            true);
    }
}
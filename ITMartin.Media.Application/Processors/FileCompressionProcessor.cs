using System.IO.Compression;

namespace ITMartin.Media.Application.Processors;

public class FileCompressionProcessor
{
    public string Compress(
        string sourcePath,
        string zipPath)
    {
        if (File.Exists(zipPath))
        {
            File.Delete(zipPath);
        }

        ZipFile.CreateFromDirectory(
            sourcePath,
            zipPath);

        return zipPath;
    }
}
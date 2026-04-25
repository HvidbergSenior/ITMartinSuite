using ITMartinImageProcessor.Domain.Interfaces;

namespace ITMartinImageProcessor.Infrastructure.Services;

public class FileService : IFileService
{
    public IEnumerable<string> GetImages(string folder)
        => Directory
            .EnumerateFiles(folder, "*.*", SearchOption.AllDirectories)
            .Where(f =>
                f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase));

    public DateTime GetCreated(string path)
        => File.GetCreationTimeUtc(path);

    public void Move(string source, string destination)
    {
        var dir = Path.GetDirectoryName(destination);

        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir!);

        File.Move(source, destination, true);
    }
}
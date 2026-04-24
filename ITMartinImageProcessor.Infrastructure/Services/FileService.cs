using ITMartinImageProcessor.Domain.Interfaces;

namespace ITMartinImageProcessor.Infrastructure.Services;

public class FileService : IFileService
{
    public IEnumerable<string> GetImages(string folder)
        => Directory
            .EnumerateFiles(folder, "*.*", SearchOption.TopDirectoryOnly)
            .Where(f =>
                f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                f.EndsWith(".heic", StringComparison.OrdinalIgnoreCase) ||
                f.EndsWith(".heif", StringComparison.OrdinalIgnoreCase));

    public DateTime GetCreated(string path)
        => File.GetCreationTime(path);

    public void Move(string source, string destination)
        => File.Move(source, destination, true);
}
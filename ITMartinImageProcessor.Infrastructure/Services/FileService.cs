using ITMartinImageProcessor.Domain.Interfaces;

namespace ITMartinImageProcessor.Infrastructure.Services;

public class FileService : IFileService
{
    public IEnumerable<string> GetImages(string folder)
    {
        if (!Directory.Exists(folder))
        {
            Console.WriteLine($"[WARNING] Folder missing: {folder}");
            return Enumerable.Empty<string>();
        }

        return Directory
            .EnumerateFiles(folder, "*.*", SearchOption.TopDirectoryOnly)
            .Where(f =>
            {
                var isValid =
                    f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                    f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase);

                if (!isValid)
                    Console.WriteLine($"[SKIP] Not jpg: {f}");

                return isValid;
            });
    }

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
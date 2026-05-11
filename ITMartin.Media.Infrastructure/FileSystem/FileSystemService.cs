
using ITMartin.Media.Interfaces;

namespace ITMartin.Media.Infrastructure.FileSystem;

public class FileSystemService : IFileSystem
{
    public IEnumerable<string> EnumerateFiles(
        string root,
        string searchPattern,
        EnumerationOptions options)
        => Directory.EnumerateFiles(root, searchPattern, options);

    public bool DirectoryExists(string path)
        => Directory.Exists(path);

    public long GetFileSize(string path)
        => new FileInfo(path).Length;

    public DateTime GetLastWriteTime(string path)
        => File.GetLastWriteTime(path);
}
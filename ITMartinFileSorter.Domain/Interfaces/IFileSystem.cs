namespace ITMartinFileSorter.Domain.Interfaces;

public interface IFileSystem
{
    IEnumerable<string> EnumerateFiles(
        string root,
        string searchPattern,
        EnumerationOptions options);

    bool DirectoryExists(string path);

    long GetFileSize(string path);

    DateTime GetLastWriteTime(string path);
}
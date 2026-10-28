using ITMartinGallery.Application;

namespace ITMartinGallery.Infrastructure;

// The libraries as they are on the NAS (mounted read-only into the container).
public sealed class LibraryDisk : ILibraryDisk
{
    public bool DirectoryExists(string dir) => Directory.Exists(dir);
    public bool FileExists(string file) => File.Exists(file);
    public IEnumerable<string> Directories(string dir) => Directory.EnumerateDirectories(dir);
    public IEnumerable<string> Files(string dir, string pattern = "*") => Directory.EnumerateFiles(dir, pattern);
    public IEnumerable<string> AllFiles(string root, string pattern = "*") => Directory.EnumerateFiles(root, pattern, SearchOption.AllDirectories);
    public DateTime LastWriteUtc(string file) => File.GetLastWriteTimeUtc(file);
    public long Length(string file) => new FileInfo(file).Length;
    public string? ReadText(string file) => File.Exists(file) ? File.ReadAllText(file) : null;

    public string Md5(string file)
    {
        using var stream = File.OpenRead(file);
        return Convert.ToHexString(System.Security.Cryptography.MD5.HashData(stream));
    }

    public string? LinkTarget(string file)
    {
        if (!File.Exists(file) || new FileInfo(file).LinkTarget is null) return null;
        return (File.ResolveLinkTarget(file, returnFinalTarget: true) as FileInfo)?.FullName ?? "";
    }
}

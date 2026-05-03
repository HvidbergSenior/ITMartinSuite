using ITMartinFileSorter.Domain.Entities;

namespace ITMartinFileSorter.Domain.Interfaces;

public interface IFileScanner
{
    // 🔹 Fast path discovery (no heavy work)
    IEnumerable<string> EnumerateFiles(string rootPath);

    // 🔹 Process a single file (parallel-safe)
    MediaFile? ProcessFile(string path);

    // 🔹 Legacy / compatibility (optional)
    IEnumerable<MediaFile> ScanFolder(
        string rootPath,
        Action<int, string>? onProgress = null);
}
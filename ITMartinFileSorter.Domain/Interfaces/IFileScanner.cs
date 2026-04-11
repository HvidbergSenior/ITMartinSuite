using ITMartinFileSorter.Domain.Entities;

namespace ITMartinFileSorter.Domain.Interfaces;

public interface IFileScanner
{
    IEnumerable<MediaFile> ScanFolder(string rootPath, Action<int, string>? onProgress = null);
}
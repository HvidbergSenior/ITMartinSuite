using ITMartin.Media.Domain.Entities;

namespace ITMartinFileSorter.Application.Interfaces;

public interface ILibraryExportService
{
    Task ExportAsync(
        IEnumerable<MediaFile> files,
        string root,
        Func<int, int, string, string, Task>? progress);

}
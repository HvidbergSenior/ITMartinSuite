    namespace ITMartinFileSorter.Application.Interfaces;

    public interface IFolderPathInfoService
    {
        string? GetPathInfo(
            string? configuredPath,
            string? selectedPath);
    }
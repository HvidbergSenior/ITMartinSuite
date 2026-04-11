    namespace ITMartinFileSorter.Application.Services;

    public class FolderPathInfoService
    {
        public string? GetPathInfo(
            string? configuredPath,
            string? selectedPath)
        {
            if (string.IsNullOrWhiteSpace(configuredPath) ||
                string.IsNullOrWhiteSpace(selectedPath))
                return null;

            var configFull = Path.GetFullPath(configuredPath)
                .TrimEnd(Path.DirectorySeparatorChar);

            var selectedFull = Path.GetFullPath(selectedPath)
                .TrimEnd(Path.DirectorySeparatorChar);

            if (!string.Equals(
                    configFull,
                    selectedFull,
                    StringComparison.OrdinalIgnoreCase))
            {
                return
                    $"You are using a different folder than the default library.\n" +
                    $"Default: {configFull}\n" +
                    $"Selected: {selectedFull}";
            }

            return null;
        }
    }
namespace ITMartin.Media.Application.Processors;

public class EmptyFolderProcessor
{
    public List<string> Process(
        IEnumerable<string> folders)
    {
        return folders
            .Where(path =>
                Directory.Exists(path)
                &&
                !Directory
                    .EnumerateFileSystemEntries(
                        path)
                    .Any())
            .ToList();
    }
}
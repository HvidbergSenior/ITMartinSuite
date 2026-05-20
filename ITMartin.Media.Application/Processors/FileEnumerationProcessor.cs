using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;

namespace ITMartin.Media.Application.Processors;

public class FileEnumerationProcessor
{
    private readonly IFileScanner
        _fileScanner;

    public FileEnumerationProcessor(
        IFileScanner fileScanner)
    {
        _fileScanner =
            fileScanner;
    }

    public List<string>
        Enumerate(
            string folderPath)
    {
        return _fileScanner
            .EnumerateFiles(folderPath)
            .ToList();
    }
}
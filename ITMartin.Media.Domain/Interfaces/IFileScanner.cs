using ITMartin.Media.Domain.Entities;
using ITMartin.Media.Domain.Enums;

namespace ITMartin.Media.Domain.Interfaces;

public interface IFileScanner
{
    Task<IEnumerable<string>> ScanAsync(
        string rootPath,
        CancellationToken cancellationToken);

    IEnumerable<string> EnumerateFiles(
        string rootPath);

    MediaFile? ProcessFile(
        string path,
        ScanMode scanMode);
}
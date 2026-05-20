
using ITMartin.Media.Contracts.Contracts.Runtime.Enums;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;

namespace ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;

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
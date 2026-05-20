using ITMartin.Media.Contracts.Contracts.Runtime.Models;

namespace ITMartin.Media.Application.Abstractions.Strategies.Export;

public interface IExportStrategy
{
    string Name { get; }

    Task ExportAsync(
        IReadOnlyCollection<MediaFile> files,
        string destination,
        CancellationToken cancellationToken);
}
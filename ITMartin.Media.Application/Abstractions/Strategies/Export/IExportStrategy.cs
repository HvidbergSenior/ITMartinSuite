using ITMartin.Media.Domain.Entities;

namespace ITMartin.Media.Application.Abstractions.Strategies.Export;

public interface IExportStrategy
{
    string Name { get; }

    Task ExportAsync(
        IReadOnlyCollection<MediaFile> files,
        string destination,
        CancellationToken cancellationToken);
}
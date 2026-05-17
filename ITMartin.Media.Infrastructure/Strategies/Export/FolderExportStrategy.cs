using ITMartin.Media.Application.Abstractions.Strategies.Export;

namespace ITMartin.Media.Infrastructure.Strategies.Export;

using ITMartin.Media.Domain.Entities;

public sealed class FolderExportStrategy
    : IExportStrategy
{
    public string Name => "Folder";

    public async Task ExportAsync(
        IReadOnlyCollection<MediaFile> files,
        string destination,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(destination);

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var target =
                Path.Combine(
                    destination,
                    file.FileName);

            await using var sourceStream =
                File.OpenRead(file.FullPath);

            await using var destinationStream =
                File.Create(target);

            await sourceStream.CopyToAsync(
                destinationStream,
                cancellationToken);
        }
    }
}
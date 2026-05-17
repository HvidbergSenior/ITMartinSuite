namespace ITMartin.Media.Infrastructure.Indexing;

using ITMartin.Media.Application.Abstractions.Indexing;
using ITMartin.Media.Domain.Entities;

public sealed class InMemoryMediaIndexService
    : IMediaIndexService
{
    private static readonly List<MediaIndexEntity>
        Index = [];

    public Task IndexAsync(
        MediaFile mediaFile,
        CancellationToken cancellationToken)
    {
        Index.Add(
            new MediaIndexEntity
            {
                Id = Guid.NewGuid(),
                FilePath = mediaFile.FullPath,
                FileName = mediaFile.FileName,
                FileSize = mediaFile.SizeBytes,
                Sha256 = mediaFile.Hash ?? "",
                IndexedAt = DateTimeOffset.UtcNow
            });

        return Task.CompletedTask;
    }

    public Task<IReadOnlyCollection<MediaIndexEntity>>
        SearchAsync(
            string query,
            CancellationToken cancellationToken)
    {
        IReadOnlyCollection<MediaIndexEntity>
            results =
                Index
                    .Where(x =>
                        x.FileName.Contains(
                            query,
                            StringComparison.OrdinalIgnoreCase))
                    .ToList();

        return Task.FromResult(results);
    }
}
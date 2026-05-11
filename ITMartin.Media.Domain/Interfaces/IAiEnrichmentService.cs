using ITMartin.Media.Domain.Entities;

namespace ITMartin.Media.Domain.Interfaces;

public interface IAiEnrichmentService
{
    Task EnrichBatchAsync(
        List<MediaFile> files,
        Func<Task>? onBatchCompleted = null,
        CancellationToken cancellationToken = default);

    Task<string> TestAsync();
}
using ITMartin.Media.Contracts.Contracts.Runtime.Models;

namespace ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;

public interface IAiEnrichmentService
{
    Task EnrichBatchAsync(
        List<MediaFile> files,
        Func<Task>? onBatchCompleted = null,
        CancellationToken cancellationToken = default);

    Task<string> TestAsync();

    /// <summary>
    /// Text-only classification for Unhandled files (unrecognized extensions) -
    /// no image bytes sent, just filename/relative path, so batches can be much
    /// larger and cheaper than EnrichBatchAsync's vision-based batches.
    /// </summary>
    Task<List<UnhandledClassificationItem>> ClassifyUnhandledBatchAsync(
        List<(Guid Id, string RelativePath)> items,
        CancellationToken cancellationToken = default);
}
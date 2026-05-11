using ITMartin.Media.Entities;

namespace ITMartin.Media.Interfaces;

public interface IAiEnrichmentService
{
    Task EnrichBatchAsync(
        List<MediaFile> files,
        Func<Task>? onBatchCompleted = null);

    Task<string> TestAsync();
}
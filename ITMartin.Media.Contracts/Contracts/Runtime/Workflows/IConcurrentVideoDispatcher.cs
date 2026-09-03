using ITMartin.Media.Contracts.Contracts.Runtime.Models;

namespace ITMartin.Media.Contracts.Contracts.Runtime.Workflows;

/// <summary>
/// Fires off a video's ffmpeg conversion the moment it's classified, instead
/// of QuickSort waiting for a dedicated normalization step - the conversion
/// then races concurrently against QuickSort's own remaining steps (hash,
/// dedup, metadata, export...). By the time Export runs for a given file,
/// this may or may not have finished; if it hasn't, Export just uses the
/// original (same NormalizedPath ?? FullPath fallback as always) and
/// VideoConvertFinalizeWorkflowStep swaps the converted file in afterward
/// once it's ready. Scoped per workflow run (one instance per job).
/// </summary>
public interface IConcurrentVideoDispatcher
{
    void Dispatch(MediaFile file, CancellationToken cancellationToken);

    IReadOnlyList<(MediaFile File, Task ConversionTask)> GetPending();
}

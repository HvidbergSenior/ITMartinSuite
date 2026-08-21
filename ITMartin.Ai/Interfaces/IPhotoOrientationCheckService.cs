using ITMartin.Ai.Models;

namespace ITMartin.Ai.Interfaces;

// AI-vision alternative to the free face-detection rotation check
// (ILibraryPolishService.DetectRotatedImagesAsync) - a single look tells
// orientation directly, unlike the free method which needs a clear face and
// 4 separate inference passes per photo. Real Claude API cost, so callers
// must batch (see CheckBatchAsync) and cap total images per run.
public interface IPhotoOrientationCheckService
{
    // imagePaths.Count should not exceed BatchSize (20) - the caller is
    // responsible for chunking a larger list and enforcing its own overall
    // cap, same convention as every other per-photo AI service in this
    // codebase (see feedback_ai_cost_ceiling / feedback_addon_speed memory).
    Task<List<PhotoOrientationResult>> CheckBatchAsync(
        IReadOnlyList<(string FullPath, string RelativePath)> images,
        CancellationToken cancellationToken = default);
}

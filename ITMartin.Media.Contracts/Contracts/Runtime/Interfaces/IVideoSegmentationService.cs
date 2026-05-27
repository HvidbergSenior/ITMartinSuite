using ITMartin.Media.Contracts.Contracts.Runtime.Models;

namespace ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;

public interface IVideoSegmentationService
{
    Task<List<MediaSegment>> DetectSegmentsAsync(
        string videoPath,
        CancellationToken cancellationToken = default);

    Task GenerateSampleAsync(
        string inputPath,
        string outputPath,
        TimeSpan start,
        TimeSpan duration,
        CancellationToken cancellationToken = default);
}
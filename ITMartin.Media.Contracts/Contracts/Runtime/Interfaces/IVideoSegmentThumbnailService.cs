namespace ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;

public interface IVideoSegmentThumbnailService
{
    Task<string?> GenerateThumbnailAsync(
        string videoPath,
        TimeSpan timestamp,
        CancellationToken cancellationToken = default);
}
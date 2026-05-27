namespace ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;

public interface IVideoSampleService
{
    Task<string> CreateSampleAsync(
        string inputPath,
        TimeSpan start,
        TimeSpan duration,
        CancellationToken cancellationToken = default);
}
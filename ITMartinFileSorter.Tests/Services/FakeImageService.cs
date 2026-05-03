using ITMartinFileSorter.Domain.Interfaces;

namespace ITMartinFileSorter.Tests.Services;

public class FakeImageService : IImageBatchService
{
    public bool Called { get; private set; }

    public Task ConvertAllImagesAsync(
        string exportRoot,
        Action<int, int, string>? progress = null)
    {
        Called = true;
        return Task.CompletedTask;
    }

    public Task ConvertAllImagesAsync(IEnumerable<MediaFile> files, Action<int, int, string>? progress = null)
    {
        throw new NotImplementedException();
    }
}
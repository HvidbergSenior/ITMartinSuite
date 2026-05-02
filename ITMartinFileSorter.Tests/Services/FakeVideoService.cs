using ITMartinFileSorter.Domain.Interfaces;

namespace ITMartinFileSorter.Tests.Services;

public class FakeVideoService : IVideoBatchService
{
    public bool Called { get; private set; }

    public Task ConvertAllVideosAsync(
        string exportRoot,
        Action<int, int, string>? progress = null)
    {
        Called = true;
        return Task.CompletedTask;
    }

}
namespace ITMartin.Media.Application.Processors;

public class FileThrottleProcessor
{
    private readonly SemaphoreSlim
        _semaphore;

    public FileThrottleProcessor(
        int maxConcurrency = 4)
    {
        _semaphore =
            new SemaphoreSlim(
                maxConcurrency);
    }

    public async Task RunAsync(
        Func<Task> action)
    {
        await _semaphore.WaitAsync();

        try
        {
            await action();
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
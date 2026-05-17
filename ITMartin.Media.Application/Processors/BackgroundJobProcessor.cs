namespace ITMartin.Media.Application.Processors;

public class BackgroundJobProcessor
{
    public Task Run(
        Func<Task> action)
    {
        return Task.Run(action);
    }
}
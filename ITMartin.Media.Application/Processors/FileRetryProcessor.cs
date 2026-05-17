namespace ITMartin.Media.Application.Processors;

public class FileRetryProcessor
{
    public async Task<bool> ExecuteAsync(
        Func<Task> action,
        int retries = 3,
        int delayMs = 500)
    {
        for (int i = 0;
             i < retries;
             i++)
        {
            try
            {
                await action();

                return true;
            }
            catch
            {
                if (i == retries - 1)
                {
                    return false;
                }

                await Task.Delay(
                    delayMs);
            }
        }

        return false;
    }
}
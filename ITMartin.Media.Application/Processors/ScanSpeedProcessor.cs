namespace ITMartin.Media.Application.Processors;

public class ScanSpeedProcessor
{
    public double Calculate(
        int processed,
        TimeSpan elapsed)
    {
        if (elapsed.TotalSeconds <= 0)
        {
            return 0;
        }

        return processed /
               elapsed.TotalSeconds;
    }
}
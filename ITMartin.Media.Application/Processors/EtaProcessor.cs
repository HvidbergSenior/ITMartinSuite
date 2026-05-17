namespace ITMartin.Media.Application.Processors;

public class EtaProcessor
{
    public TimeSpan Calculate(
        int processed,
        int total,
        double speed)
    {
        if (speed <= 0)
        {
            return TimeSpan.Zero;
        }

        var remaining =
            total - processed;

        return TimeSpan.FromSeconds(
            remaining / speed);
    }
}
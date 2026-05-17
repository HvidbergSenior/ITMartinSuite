namespace ITMartin.Media.Application.Processors;

public class BatchProgressProcessor
{
    public double Calculate(
        int current,
        int total)
    {
        if (total <= 0)
        {
            return 0;
        }

        return (double)current /
            total * 100;
    }
}
namespace ITMartin.Media.Application.Processors;

public class ParallelOptionsProcessor
{
    public ParallelOptions Create(
        int degree)
    {
        return new ParallelOptions
        {
            MaxDegreeOfParallelism =
                degree
        };
    }
}
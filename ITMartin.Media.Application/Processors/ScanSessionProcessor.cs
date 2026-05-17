namespace ITMartin.Media.Application.Processors;

public class ScanSessionProcessor
{
    public Guid CreateId()
    {
        return Guid.NewGuid();
    }

    public DateTime Created()
    {
        return DateTime.UtcNow;
    }
}
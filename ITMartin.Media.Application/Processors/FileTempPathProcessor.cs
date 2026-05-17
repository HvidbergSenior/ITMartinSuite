namespace ITMartin.Media.Application.Processors;

public class FileTempPathProcessor
{
    public string Build(
        string extension)
    {
        return Path.Combine(
            Path.GetTempPath(),
            $"{Guid.NewGuid()}{extension}");
    }
}
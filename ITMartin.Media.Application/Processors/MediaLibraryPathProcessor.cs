namespace ITMartin.Media.Application.Processors;

public class MediaLibraryPathProcessor
{
    public string Build(
        string root,
        int year,
        int month)
    {
        return Path.Combine(
            root,
            year.ToString(),
            month.ToString("00"));
    }
}
namespace ITMartin.Media.Application.Processors;

public class DirectoryExistsProcessor
{
    public bool Exists(
        string path)
    {
        return Directory.Exists(
            path);
    }
}
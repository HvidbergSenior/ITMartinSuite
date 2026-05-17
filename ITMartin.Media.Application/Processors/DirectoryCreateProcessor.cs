namespace ITMartin.Media.Application.Processors;

public class DirectoryCreateProcessor
{
    public void Create(
        string path)
    {
        Directory.CreateDirectory(
            path);
    }
}
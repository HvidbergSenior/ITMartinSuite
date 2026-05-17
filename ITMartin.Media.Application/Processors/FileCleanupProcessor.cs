namespace ITMartin.Media.Application.Processors;

public class FileCleanupProcessor
{
    public void Cleanup(
        string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
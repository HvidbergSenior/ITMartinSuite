namespace ITMartin.Media.Application.Processors;

public class DirectoryDeleteProcessor
{
    public void Delete(
        string path,
        bool recursive = false)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        Directory.Delete(
            path,
            recursive);
    }
}
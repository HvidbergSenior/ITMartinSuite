namespace ITMartin.Media.Application.Processors;

public class FolderSizeProcessor
{
    public long Calculate(
        string path)
    {
        if (!Directory.Exists(path))
        {
            return 0;
        }

        return Directory
            .EnumerateFiles(
                path,
                "*.*",
                SearchOption
                    .AllDirectories)
            .Sum(file =>
            {
                try
                {
                    return new FileInfo(file)
                        .Length;
                }
                catch
                {
                    return 0;
                }
            });
    }
}
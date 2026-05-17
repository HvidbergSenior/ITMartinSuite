using ITMartin.Media.Domain.Entities;

namespace ITMartin.Media.Application.Processors;

public class FileAccessProcessor
{
    public bool CanRead(
        MediaFile file)
    {
        try
        {
            using var stream =
                File.OpenRead(
                    file.FullPath);

            return true;
        }
        catch
        {
            return false;
        }
    }

    public bool CanWrite(
        string path)
    {
        try
        {
            using var stream =
                File.Open(
                    path,
                    FileMode.OpenOrCreate,
                    FileAccess.Write);

            return true;
        }
        catch
        {
            return false;
        }
    }
}
using ITMartin.Media.Domain.Entities;

namespace ITMartin.Media.Application.Processors;

public class FileLockProcessor
{
    public bool IsLocked(
        MediaFile file)
    {
        try
        {
            using var stream =
                File.Open(
                    file.FullPath,
                    FileMode.Open,
                    FileAccess.ReadWrite,
                    FileShare.None);

            return false;
        }
        catch
        {
            return true;
        }
    }
}
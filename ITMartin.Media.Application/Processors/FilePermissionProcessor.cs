using ITMartin.Media.Contracts.Contracts.Runtime.Models;

namespace ITMartin.Media.Application.Processors;

public class FilePermissionProcessor
{
    public bool IsReadOnly(
        MediaFile file)
    {
        return new FileInfo(
                file.FullPath)
            .IsReadOnly;
    }
}
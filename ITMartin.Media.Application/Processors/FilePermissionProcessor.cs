using ITMartin.Media.Domain.Entities;

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
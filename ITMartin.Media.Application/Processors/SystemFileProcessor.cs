using ITMartin.Media.Domain.Entities;

namespace ITMartin.Media.Application.Processors;

public class SystemFileProcessor
{
    public bool IsSystem(
        MediaFile file)
    {
        return File.GetAttributes(
                file.FullPath)
            .HasFlag(
                FileAttributes.System);
    }
}
using ITMartin.Media.Contracts.Contracts.Runtime.Models;

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
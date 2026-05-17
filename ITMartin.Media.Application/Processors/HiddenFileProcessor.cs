using ITMartin.Media.Domain.Entities;

namespace ITMartin.Media.Application.Processors;

public class HiddenFileProcessor
{
    public bool IsHidden(
        MediaFile file)
    {
        return File.GetAttributes(
                file.FullPath)
            .HasFlag(
                FileAttributes.Hidden);
    }
}
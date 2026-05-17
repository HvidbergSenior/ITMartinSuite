using ITMartin.Media.Domain.Entities;

namespace ITMartin.Media.Application.Processors;

public class MediaDimensionProcessor
{
    public bool HasDimensions(
        MediaFile file)
    {
        return file.Width.HasValue &&
               file.Height.HasValue;
    }

    public (int? Width, int? Height)
        Get(
            MediaFile file)
    {
        return (
            file.Width,
            file.Height);
    }

    public void Set(
        MediaFile file,
        int? width,
        int? height)
    {
        file.Width =
            width;

        file.Height =
            height;
    }
}
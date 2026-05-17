using ITMartin.Media.Domain.Entities;

namespace ITMartin.Media.Application.Processors;

public class ExifProcessor
{
    public bool HasExif(
        MediaFile file)
    {
        return file.HasExif;
    }

    public void Set(
        MediaFile file,
        bool value)
    {
        file.HasExif =
            value;
    }
}
using ITMartin.Media.Domain.Entities;

namespace ITMartin.Media.Application.Processors;

public class RealPhotoProcessor
{
    public bool IsRealPhoto(
        MediaFile file)
    {
        return file.IsProbablyRealPhoto;
    }

    public void Set(
        MediaFile file,
        bool value)
    {
        file.IsProbablyRealPhoto =
            value;
    }
}
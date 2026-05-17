using ITMartin.Media.Domain.Entities;

namespace ITMartin.Media.Application.Processors;

public class ThumbnailPathProcessor
{
    public string Build(
        MediaFile file,
        string root)
    {
        return Path.Combine(
            root,
            $"{file.Id}.jpg");
    }
}
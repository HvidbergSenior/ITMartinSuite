using ITMartin.Media.Application.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Enums;

namespace ITMartin.Media.Infrastructure.Media;

public sealed class MediaTypeResolver
    : IMediaTypeResolver
{
    public MediaType Resolve(
        string path)
    {
        return MediaTypeHelper
            .GetMediaType(path);
    }
}
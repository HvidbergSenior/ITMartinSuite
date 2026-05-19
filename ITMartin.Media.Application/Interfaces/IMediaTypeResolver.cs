using ITMartin.Media.Enums;

namespace ITMartin.Media.Application.Interfaces;

public interface IMediaTypeResolver
{
    MediaType Resolve(string path);
}
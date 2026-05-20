using ITMartin.Media.Contracts.Contracts.Runtime.Enums;

namespace ITMartin.Media.Application.Interfaces;

public interface IMediaTypeResolver
{
    MediaType Resolve(string path);
}
using ITMartin.Media.Contracts.Contracts.Runtime.Requests.Package4;

namespace ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;

public interface IPackage4Client
{
    Task<Guid> StartAsync(
        StartPackage4Request request,
        CancellationToken cancellationToken);
}

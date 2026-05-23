using ITMartin.Media.Contracts.Contracts.Runtime.Requests;
using ITMartin.Media.Contracts.Contracts.Runtime.Requests.Package1;

namespace ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;

public interface IPackage1Client
{
    Task<Guid> StartAsync(
        StartPackage1Request request,
        CancellationToken cancellationToken);
}
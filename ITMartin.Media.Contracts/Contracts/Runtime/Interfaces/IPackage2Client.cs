using ITMartin.Media.Contracts.Contracts.Runtime.Requests;
using ITMartin.Media.Contracts.Contracts.Runtime.Requests.Package2;

namespace ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;

public interface IPackage2Client
{
    Task<Guid> StartAsync(
        StartPackage2Request request,
        CancellationToken cancellationToken);
}
using ITMartin.Media.Contracts.Contracts.Runtime.Requests;

namespace ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;

public interface IPackage2Client
{
    Task StartAsync(
        StartPackage2Request request,
        CancellationToken cancellationToken);
}
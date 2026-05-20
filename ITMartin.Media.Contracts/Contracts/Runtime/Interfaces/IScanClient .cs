using ITMartin.Media.Contracts.Contracts.Runtime.Models;

namespace ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;

public interface IScanClient
{
    Task<Guid> StartAsync(
        StartScanRequest request,
        CancellationToken cancellationToken);
}
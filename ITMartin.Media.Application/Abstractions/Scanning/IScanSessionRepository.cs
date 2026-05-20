using ITMartin.Media.Application.Models.Scanning;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;

namespace ITMartin.Media.Application.Abstractions.Scanning;

public interface IScanSessionRepository
{
    Task CreateAsync(
        ScanSession session,
        CancellationToken cancellationToken);

    Task<ScanSession?> GetAsync(
        Guid id,
        CancellationToken cancellationToken);

    Task UpdateAsync(
        ScanSession session,
        CancellationToken cancellationToken);
}
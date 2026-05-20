using ITMartin.Media.Contracts.Contracts.Runtime.Models;

namespace ITMartin.Media.Contracts.Contracts.Runtime.Persistence;

public interface IScanSessionStore
{
    Task CreateAsync(
        ScanSession session,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(
        ScanSession session,
        CancellationToken cancellationToken = default);

    Task<ScanSession?> GetAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}
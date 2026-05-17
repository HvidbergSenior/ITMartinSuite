using ITMartin.Media.Domain.Entities;

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
using ITMartin.Media.Application.Models.Scanning;
using ITMartin.Media.Domain.Entities;

namespace ITMartin.Media.Application.Abstractions.Scanning;

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
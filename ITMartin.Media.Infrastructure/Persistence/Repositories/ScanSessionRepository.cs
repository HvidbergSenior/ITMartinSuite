using ITMartin.Media.Application.Abstractions.Scanning;
using ITMartin.Media.Domain.Entities;

namespace ITMartin.Media.Infrastructure.Persistence.Repositories;

public sealed class ScanSessionRepository
    : IScanSessionRepository
{
    private static readonly List<ScanSession> Sessions = [];

    public Task CreateAsync(
        ScanSession session,
        CancellationToken cancellationToken)
    {
        Sessions.Add(session);

        return Task.CompletedTask;
    }

    public Task<ScanSession?> GetAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var session = Sessions.FirstOrDefault(x => x.Id == id);

        return Task.FromResult(session);
    }

    public Task UpdateAsync(
        ScanSession session,
        CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
using ITMartin.Media.Application.Abstractions.Scanning;
using ITMartin.Media.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace ITMartin.Media.Infrastructure.Persistence.Stores;

public sealed class EfScanSessionStore
    : IScanSessionStore
{
    private readonly MediaDbContext _dbContext;

    public EfScanSessionStore(MediaDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task CreateAsync(
        Application.Models.Scanning.ScanSession session,
        CancellationToken cancellationToken = default)
    {
        var entity = new ScanSessionEntity
        {
            Id = session.Id,
            RootPath = session.RootPath,
            Status = session.Status,
            TotalFiles = session.TotalFiles,
            ProcessedFiles = session.ProcessedFiles,
            StartedAtUtc = session.StartedAtUtc,
            CompletedAtUtc = session.CompletedAtUtc
        };

        await _dbContext.ScanSessions.AddAsync(entity, cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(
        Application.Models.Scanning.ScanSession session,
        CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.ScanSessions
            .FirstAsync(x => x.Id == session.Id, cancellationToken);

        entity.Status = session.Status;
        entity.TotalFiles = session.TotalFiles;
        entity.ProcessedFiles = session.ProcessedFiles;
        entity.CompletedAtUtc = session.CompletedAtUtc;

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<Application.Models.Scanning.ScanSession?> GetAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.ScanSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null)
        {
            return null;
        }

        return new Application.Models.Scanning.ScanSession
        {
            Id = entity.Id,
            RootPath = entity.RootPath,
            Status = entity.Status,
            TotalFiles = entity.TotalFiles,
            ProcessedFiles = entity.ProcessedFiles,
            StartedAtUtc = entity.StartedAtUtc,
            CompletedAtUtc = entity.CompletedAtUtc
        };
    }
}
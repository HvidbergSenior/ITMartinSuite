using System.Text.Json;
using ITMartin.Media.Application.Pipelines.Package1.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace ITMartin.Media.Infrastructure.Persistence.Stores;

public sealed class EfPackage1ManifestStore(
    MediaDbContext dbContext)
    : IPackage1ManifestStore
{
    public async Task SaveAsync(
        Package1Manifest manifest,
        CancellationToken cancellationToken = default)
    {
        var existing =
            await dbContext.Package1Manifests
                .FirstOrDefaultAsync(
                    x => x.WorkflowId ==
                         manifest.WorkflowId,
                    cancellationToken);

        if (existing is null)
        {
            existing =
                new Package1ManifestEntity
                {
                    WorkflowId =
                        manifest.WorkflowId,

                    RootPath =
                        manifest.RootPath,

                    FileCount =
                        manifest.FileCount,

                    MediaFilesJson =
                        JsonSerializer.Serialize(
                            manifest.MediaFiles),

                    CreatedAtUtc =
                        manifest.CreatedAtUtc
                };

            dbContext.Package1Manifests.Add(
                existing);
        }

        existing.RootPath =
            manifest.RootPath;

        existing.FileCount =
            manifest.FileCount;

        existing.MediaFilesJson =
            JsonSerializer.Serialize(
                manifest.MediaFiles);

        existing.CreatedAtUtc =
            manifest.CreatedAtUtc;

        await dbContext.SaveChangesAsync(
            cancellationToken);
    }

    public async Task<Package1Manifest?> GetAsync(
        Guid workflowId,
        CancellationToken cancellationToken = default)
    {
        var entity =
            await dbContext.Package1Manifests
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.WorkflowId == workflowId,
                    cancellationToken);

        if (entity is null)
        {
            return null;
        }

        var mediaFiles =
            JsonSerializer.Deserialize<
                List<MediaFile>>(
                    entity.MediaFilesJson)
            ?? [];

        return new Package1Manifest
        {
            WorkflowId =
                entity.WorkflowId,

            RootPath =
                entity.RootPath,

            FileCount =
                entity.FileCount,

            MediaFiles =
                mediaFiles,

            CreatedAtUtc =
                entity.CreatedAtUtc
        };
    }
}
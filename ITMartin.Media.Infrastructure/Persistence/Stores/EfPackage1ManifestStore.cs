// File:
// ITMartin.Media.Infrastructure/Persistence/Stores/EfPackage1ManifestStore.cs

using System.Text.Json;
using ITMartin.Media.Application.Pipelines.Package1.Models;
using ITMartin.Media.Domain.Entities;
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
        var entity =
            new Package1ManifestEntity
            {
                WorkflowId = manifest.WorkflowId,
                RootPath = manifest.RootPath,
                FileCount = manifest.FileCount,

                MediaFilesJson =
                    JsonSerializer.Serialize(
                        manifest.MediaFiles),

                CreatedAtUtc =
                    manifest.CreatedAtUtc
            };

        dbContext.Package1Manifests.Add(
            entity);

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
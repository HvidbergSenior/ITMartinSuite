using System.Text.Json;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Persistence;
using ITMartin.Media.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace ITMartin.Media.Infrastructure.Persistence.Stores;

public sealed class EfPackage2ManifestStore(
    MediaDbContext dbContext)
    : IPackage2ManifestStore
{
    public async Task SaveAsync(
        Package2Manifest manifest,
        CancellationToken cancellationToken = default)
    {
        var existing =
            await dbContext.Package2Manifests
                .FirstOrDefaultAsync(
                    x => x.PackageId ==
                         manifest.PackageId,
                    cancellationToken);

        if (existing is null)
        {
            existing =
                new Package2ManifestEntity
                {
                    WorkflowId =
                        manifest.WorkflowId,

                    PackageId =
                        manifest.PackageId,

                    FileCount =
                        manifest.FileCount,

                    Profile =
                        manifest.Profile.ToString(),

                    ItemsJson =
                        JsonSerializer.Serialize(
                            manifest.Items),

                    CreatedAtUtc =
                        manifest.CreatedAtUtc
                };

            dbContext.Package2Manifests.Add(
                existing);
        }

        existing.WorkflowId =
            manifest.WorkflowId;

        existing.PackageId =
            manifest.PackageId;

        existing.FileCount =
            manifest.FileCount;

        existing.Profile =
            manifest.Profile.ToString();

        existing.ItemsJson =
            JsonSerializer.Serialize(
                manifest.Items);

        existing.CreatedAtUtc =
            manifest.CreatedAtUtc;

        await dbContext.SaveChangesAsync(
            cancellationToken);
    }

    public async Task<Package2Manifest?> GetAsync(
        Guid packageId,
        CancellationToken cancellationToken = default)
    {
        var entity =
            await dbContext.Package2Manifests
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.PackageId == packageId,
                    cancellationToken);

        if (entity is null)
        {
            return null;
        }

        var items =
            JsonSerializer.Deserialize<
                List<EnhancedMediaManifestItem>>(
                entity.ItemsJson)
            ?? [];

        return new Package2Manifest
        {
            WorkflowId =
                entity.WorkflowId,

            PackageId =
                entity.PackageId,

            FileCount =
                entity.FileCount,

            Profile =
                Enum.Parse<EnhancementProfile>(
                    entity.Profile),

            Items =
                items,

            CreatedAtUtc =
                entity.CreatedAtUtc
        };
    }
}
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Entities;
using ITMartin.Media.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace ITMartin.Media.Infrastructure.Persistence;

public sealed class MediaDbContext
    : DbContext
{
    public MediaDbContext(
        DbContextOptions<MediaDbContext> options)
        : base(options)
    {
    }
    public DbSet<AiCache> AiCache => Set<AiCache>();
    public DbSet<WorkflowInstanceEntity>
        WorkflowInstances
        => Set<WorkflowInstanceEntity>();
    public DbSet<WorkflowCheckpointEntity> WorkflowCheckpoints
        => Set<WorkflowCheckpointEntity>();
    public DbSet<WorkflowStepExecutionEntity> WorkflowStepExecutions
        => Set<WorkflowStepExecutionEntity>();
    public DbSet<ScanSessionEntity> ScanSessions
        => Set<ScanSessionEntity>();
    public DbSet<WorkflowStateSnapshot> WorkflowStateSnapshots
        => Set<WorkflowStateSnapshot>();
    public DbSet<Package1ManifestEntity>
        Package1Manifests
        => Set<Package1ManifestEntity>();
    public DbSet<Package2ManifestEntity>
        Package2Manifests
        => Set<Package2ManifestEntity>();

    public DbSet<PersonEntity> People
        => Set<PersonEntity>();
    public DbSet<PersonReferencePhotoEntity> PersonReferencePhotos
        => Set<PersonReferencePhotoEntity>();
    public DbSet<MediaFaceEntity> MediaFaces
        => Set<MediaFaceEntity>();
    public DbSet<Package3IndexStatusEntity> Package3IndexStatuses
        => Set<Package3IndexStatusEntity>();

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<AiCache>()
            .HasKey(x => x.Hash);

        modelBuilder.Entity<WorkflowCheckpointEntity>()
            .HasKey(x => x.Id);

        modelBuilder.Entity<ScanSessionEntity>()
            .HasKey(x => x.Id);
        
        modelBuilder.Entity<WorkflowStepExecutionEntity>()
            .HasKey(x => x.Id);

        modelBuilder.Entity<WorkflowStepExecutionEntity>()
            .HasIndex(x => new
            {
                x.WorkflowId,
                x.StepName,
                x.Status
            });
        modelBuilder.Entity<WorkflowInstanceEntity>()
            .HasKey(x => x.WorkflowId);
        modelBuilder.Entity<Package1ManifestEntity>()
            .HasKey(x => x.WorkflowId);
        modelBuilder.Entity<WorkflowStateSnapshot>()
            .HasKey(x => x.WorkflowId);

        modelBuilder.Entity<WorkflowStateSnapshot>()
            .Property(x => x.SerializedContext)
            .IsRequired();

        modelBuilder.Entity<WorkflowStateSnapshot>()
            .Property(x => x.UpdatedAt)
            .IsRequired();
        modelBuilder.Entity<Package2ManifestEntity>()
            .HasKey(x => x.WorkflowId);
        modelBuilder.Entity<WorkflowCheckpointEntity>()
            .HasIndex(x => new
            {
                x.WorkflowId,
                x.IsLatest
            });
        modelBuilder.Entity<WorkflowCheckpointEntity>()
            .HasIndex(x => new
            {
                x.WorkflowId,
                x.StepName
            });
        modelBuilder.Entity<WorkflowCheckpointEntity>()
            .HasIndex(x => x.CreatedAtUtc);
        modelBuilder.Entity<WorkflowCheckpointEntity>()
            .Property(x => x.StateJson)
            .IsRequired();
        modelBuilder.Entity<WorkflowCheckpointEntity>()
            .Property(x => x.WorkflowName)
            .IsRequired();
        modelBuilder.Entity<WorkflowCheckpointEntity>()
            .Property(x => x.StepName)
            .IsRequired();
        modelBuilder.Entity<WorkflowCheckpointEntity>()
            .Property(x => x.Status)
            .IsRequired();

        modelBuilder.Entity<PersonEntity>()
            .HasKey(x => x.Id);

        modelBuilder.Entity<PersonReferencePhotoEntity>()
            .HasKey(x => x.Id);
        modelBuilder.Entity<PersonReferencePhotoEntity>()
            .HasIndex(x => x.PersonId);

        modelBuilder.Entity<MediaFaceEntity>()
            .HasKey(x => x.Id);
        modelBuilder.Entity<MediaFaceEntity>()
            .HasIndex(x => x.MediaFilePath);
        modelBuilder.Entity<MediaFaceEntity>()
            .HasIndex(x => x.MatchedPersonId);

        modelBuilder.Entity<Package3IndexStatusEntity>()
            .HasKey(x => x.Id);
        modelBuilder.Entity<Package3IndexStatusEntity>()
            .HasIndex(x => x.LibraryPath);
    }
}
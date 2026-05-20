using ITMartin.Media.Infrastructure.Entities;
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
    public DbSet<Package1ManifestEntity>
        Package1Manifests
        => Set<Package1ManifestEntity>();
    public DbSet<WorkflowStateSnapshot> WorkflowStateSnapshots
        => Set<WorkflowStateSnapshot>();
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
    }
}
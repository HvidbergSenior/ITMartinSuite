// File: ITMartin.Media.Infrastructure.Persistence/MediaDbContext.cs

using ITMartin.Media.Application.Models.Scanning;
using ITMartin.Media.Infrastructure.Entities;
using ITMartin.Media.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using ScanSession = ITMartin.Media.Application.Models.Scanning.ScanSession;

namespace ITMartin.Media.Infrastructure.Persistence;

public sealed class MediaDbContext
    : DbContext
{
    public MediaDbContext(
        DbContextOptions<MediaDbContext> options)
        : base(options)
    {
    }
    public DbSet<WorkflowResumeEntity> WorkflowResumes => Set<WorkflowResumeEntity>();
    public DbSet<AiCache> AiCache => Set<AiCache>();

    public DbSet<WorkflowCheckpointEntity> WorkflowCheckpoints
        => Set<WorkflowCheckpointEntity>();
    public DbSet<WorkflowStepExecutionEntity> WorkflowStepExecutions
        => Set<WorkflowStepExecutionEntity>();
    public DbSet<ScanSessionEntity> ScanSessions
        => Set<ScanSessionEntity>();
    
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
    }
}
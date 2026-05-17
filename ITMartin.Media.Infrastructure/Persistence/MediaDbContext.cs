using ITMartin.Media.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace ITMartin.Media.Infrastructure.Persistence;

public sealed class MediaDbContext : DbContext
{
    public MediaDbContext(DbContextOptions<MediaDbContext> options)
        : base(options)
    {
    }

    public DbSet<WorkflowCheckpointEntity> WorkflowCheckpoints => Set<WorkflowCheckpointEntity>();

    public DbSet<ScanSessionEntity> ScanSessions => Set<ScanSessionEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<WorkflowCheckpointEntity>(builder =>
        {
            builder.HasKey(x => x.Id);

            builder.Property(x => x.WorkflowName)
                .HasMaxLength(256);

            builder.Property(x => x.StepName)
                .HasMaxLength(256);

            builder.HasIndex(x => x.WorkflowId);
        });

        modelBuilder.Entity<ScanSessionEntity>(builder =>
        {
            builder.HasKey(x => x.Id);

            builder.Property(x => x.RootPath)
                .HasMaxLength(2048);

            builder.Property(x => x.Status)
                .HasMaxLength(128);
        });

        base.OnModelCreating(modelBuilder);
    }
}
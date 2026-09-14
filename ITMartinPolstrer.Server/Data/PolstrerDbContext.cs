using ITMartinPolstrer.Server.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ITMartinPolstrer.Server.Data;

public sealed class PolstrerDbContext(DbContextOptions<PolstrerDbContext> options) : DbContext(options)
{
    public DbSet<Piece> Pieces => Set<Piece>();
    public DbSet<Step> Steps => Set<Step>();
    public DbSet<MediaFile> Media => Set<MediaFile>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Piece>().HasIndex(p => p.Slug).IsUnique();
        b.Entity<Piece>().Ignore(p => p.TechniqueList);
        b.Entity<Piece>().HasMany(p => p.Steps).WithOne(s => s.Piece).HasForeignKey(s => s.PieceId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<Step>().HasMany(s => s.Media).WithOne(m => m.Step).HasForeignKey(m => m.StepId).OnDelete(DeleteBehavior.Cascade);
    }
}

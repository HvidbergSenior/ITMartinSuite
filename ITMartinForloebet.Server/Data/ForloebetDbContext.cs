using ITMartinForloebet.Server.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ITMartinForloebet.Server.Data;

public sealed class ForloebetDbContext(DbContextOptions<ForloebetDbContext> options) : DbContext(options)
{
    public DbSet<Forloeb> Forloeb => Set<Forloeb>();
    public DbSet<Trin> Trin => Set<Trin>();
    public DbSet<Dokument> Dokumenter => Set<Dokument>();
    public DbSet<SiteState> SiteState => Set<SiteState>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Forloeb>().HasIndex(f => f.Slug).IsUnique();
        b.Entity<Forloeb>().HasMany(f => f.Trin).WithOne(t => t.Forloeb).HasForeignKey(t => t.ForloebId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<Forloeb>().HasMany(f => f.Dokumenter).WithOne(d => d.Forloeb).HasForeignKey(d => d.ForloebId).OnDelete(DeleteBehavior.Cascade);
    }
}

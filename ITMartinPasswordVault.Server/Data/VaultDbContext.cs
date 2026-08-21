using Microsoft.EntityFrameworkCore;

namespace ITMartinPasswordVault.Server.Data;

public sealed class VaultDbContext(DbContextOptions<VaultDbContext> options) : DbContext(options)
{
    public DbSet<VaultUser> Users => Set<VaultUser>();
    public DbSet<VaultEntry> Entries => Set<VaultEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<VaultUser>().HasIndex(u => u.Email).IsUnique();
        modelBuilder.Entity<VaultEntry>().HasIndex(e => e.UserId);
    }
}

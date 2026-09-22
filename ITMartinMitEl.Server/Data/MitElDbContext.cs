using Microsoft.EntityFrameworkCore;

namespace ITMartinMitEl.Server.Data;

public sealed class MitElDbContext(DbContextOptions<MitElDbContext> options) : DbContext(options)
{
    public DbSet<Household> Households => Set<Household>();
    public DbSet<UserAccount> Users => Set<UserAccount>();
    public DbSet<AdvisorAccess> AdvisorAccess => Set<AdvisorAccess>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<UserAccount>().HasIndex(u => u.Email).IsUnique();
        b.Entity<AdvisorAccess>().HasIndex(a => a.Code).IsUnique();
        b.Entity<AdvisorAccess>().HasIndex(a => a.HouseholdId);
    }
}

public static class MitElSchema
{
    /// <summary>Creates the tables on first start. No migrations: the schema is small and additive.</summary>
    public static void Ensure(MitElDbContext db) => db.Database.EnsureCreated();
}

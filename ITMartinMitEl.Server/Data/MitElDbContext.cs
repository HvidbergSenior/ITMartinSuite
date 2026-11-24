using Microsoft.EntityFrameworkCore;

namespace ITMartinMitEl.Server.Data;

public sealed class MitElDbContext(DbContextOptions<MitElDbContext> options) : DbContext(options)
{
    public DbSet<Household> Households => Set<Household>();
    public DbSet<UserAccount> Users => Set<UserAccount>();
    public DbSet<AdvisorAccess> AdvisorAccess => Set<AdvisorAccess>();
    public DbSet<PasswordReset> PasswordResets => Set<PasswordReset>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<UserAccount>().HasIndex(u => u.Email).IsUnique();
        b.Entity<AdvisorAccess>().HasIndex(a => a.Code).IsUnique();
        b.Entity<AdvisorAccess>().HasIndex(a => a.HouseholdId);
        b.Entity<PasswordReset>().HasIndex(r => r.TokenHash).IsUnique();
        b.Entity<PasswordReset>().HasIndex(r => r.UserId);
    }
}

public static class MitElSchema
{
    /// <summary>Creates the tables on first start. No migrations: the schema is small and additive.</summary>
    public static void Ensure(MitElDbContext db)
    {
        db.Database.EnsureCreated();
        // Tables added after the first start: EnsureCreated does nothing on an existing database, so each new table is
        // created here exactly as EF would (SQLite: Guid and DateTime as TEXT).
        db.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS "PasswordResets" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_PasswordResets" PRIMARY KEY,
                "UserId" TEXT NOT NULL,
                "TokenHash" TEXT NOT NULL,
                "CreatedAtUtc" TEXT NOT NULL,
                "ExpiresAtUtc" TEXT NOT NULL,
                "UsedAtUtc" TEXT NULL);
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_PasswordResets_TokenHash" ON "PasswordResets" ("TokenHash");
            CREATE INDEX IF NOT EXISTS "IX_PasswordResets_UserId" ON "PasswordResets" ("UserId");
            """);
    }
}

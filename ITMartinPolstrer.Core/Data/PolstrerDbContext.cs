using ITMartinPolstrer.Core.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ITMartinPolstrer.Core.Data;

public sealed class PolstrerDbContext(DbContextOptions<PolstrerDbContext> options) : DbContext(options)
{
    public DbSet<Piece> Pieces => Set<Piece>();
    public DbSet<Step> Steps => Set<Step>();
    public DbSet<MediaFile> Media => Set<MediaFile>();
    public DbSet<Comment> Comments => Set<Comment>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Piece>().HasIndex(p => p.Slug).IsUnique();
        b.Entity<Piece>().Ignore(p => p.TechniqueList);
        b.Entity<Piece>().HasMany(p => p.Steps).WithOne(s => s.Piece).HasForeignKey(s => s.PieceId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<Piece>().HasMany(p => p.Comments).WithOne(c => c.Piece).HasForeignKey(c => c.PieceId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<Step>().HasMany(s => s.Media).WithOne(m => m.Step).HasForeignKey(m => m.StepId).OnDelete(DeleteBehavior.Cascade);
    }
}

// The db was created with EnsureCreated (no migrations), so columns and
// tables added later are bolted on here at startup. Each statement is
// idempotent; an "already exists" error just means it ran before.
public static class PolstrerSchema
{
    public static void Ensure(PolstrerDbContext db)
    {
        db.Database.EnsureCreated();
        foreach (var sql in new[]
        {
            "ALTER TABLE Pieces ADD COLUMN Worker TEXT NOT NULL DEFAULT ''",
            "ALTER TABLE Pieces ADD COLUMN Category TEXT NOT NULL DEFAULT ''",
            "ALTER TABLE Pieces ADD COLUMN Material TEXT NOT NULL DEFAULT ''",
            "CREATE TABLE IF NOT EXISTS Comments (Id TEXT NOT NULL PRIMARY KEY, PieceId TEXT NOT NULL, At TEXT NOT NULL, Author TEXT NOT NULL, Text TEXT NOT NULL, FOREIGN KEY (PieceId) REFERENCES Pieces (Id) ON DELETE CASCADE)",
            "CREATE INDEX IF NOT EXISTS IX_Comments_PieceId ON Comments (PieceId)",
        })
        {
            try { db.Database.ExecuteSqlRaw(sql); }
            catch (Microsoft.Data.Sqlite.SqliteException ex) when (ex.Message.Contains("duplicate column", StringComparison.OrdinalIgnoreCase)) { }
        }
    }
}

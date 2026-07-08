using ITMartinUret.Server.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ITMartinUret.Server.Data;

public class UretDbContext(DbContextOptions<UretDbContext> options) : DbContext(options)
{
    public DbSet<Post> Posts { get; set; } = null!;
    public DbSet<PostUpdate> Updates { get; set; } = null!;
    public DbSet<Attachment> Attachments { get; set; } = null!;
    public DbSet<Report> Reports { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Post>().HasIndex(p => p.Company);
        b.Entity<Post>().HasIndex(p => p.Status);
        b.Entity<Post>().HasIndex(p => p.EditToken);
        b.Entity<PostUpdate>().HasIndex(u => u.PostId);
        b.Entity<Attachment>().HasIndex(a => a.PostId);
        b.Entity<Report>().HasIndex(r => r.Status);
    }
}

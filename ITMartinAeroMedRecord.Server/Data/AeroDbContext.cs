using ITMartinAeroMedRecord.Server.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ITMartinAeroMedRecord.Server.Data;

public sealed class AeroDbContext(DbContextOptions<AeroDbContext> options) : DbContext(options)
{
    public DbSet<Group> Groups => Set<Group>();
    public DbSet<Member> Members => Set<Member>();
    public DbSet<MemberSession> Sessions => Set<MemberSession>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<DocumentSection> Sections => Set<DocumentSection>();
    public DbSet<SectionVersion> SectionVersions => Set<SectionVersion>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<MainTask> MainTasks => Set<MainTask>();
    public DbSet<Assignment> Assignments => Set<Assignment>();
    public DbSet<Reference> References => Set<Reference>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<AeroPushSubscription> PushSubscriptions => Set<AeroPushSubscription>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Group>().HasIndex(g => g.Slug).IsUnique();
    }
}

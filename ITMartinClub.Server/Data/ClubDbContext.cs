using ITMartinClub.Server.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ITMartinClub.Server.Data;

public sealed class ClubDbContext(DbContextOptions<ClubDbContext> options) : DbContext(options)
{
    public DbSet<Group> Groups => Set<Group>();
    public DbSet<Member> Members => Set<Member>();
    public DbSet<MemberSession> Sessions => Set<MemberSession>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<DocumentRead> DocumentReads => Set<DocumentRead>();
    public DbSet<CalendarEvent> Events => Set<CalendarEvent>();
    public DbSet<Assignment> Assignments => Set<Assignment>();
    public DbSet<BulletinPost> Posts => Set<BulletinPost>();
    public DbSet<ClubChatMessage> Chat => Set<ClubChatMessage>();
    public DbSet<ClubPushSubscription> PushSubscriptions => Set<ClubPushSubscription>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Group>().HasIndex(g => g.Slug).IsUnique();

        b.Entity<DocumentRead>()
            .HasIndex(r => new { r.DocumentId, r.MemberId })
            .IsUnique();
    }
}

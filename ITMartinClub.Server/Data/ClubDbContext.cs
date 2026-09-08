using ITMartinClub.Server.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ITMartinClub.Server.Data;

public sealed class ClubDbContext(DbContextOptions<ClubDbContext> options) : DbContext(options)
{
    public DbSet<Group> Groups => Set<Group>();
    public DbSet<Member> Members => Set<Member>();
    public DbSet<MemberSession> Sessions => Set<MemberSession>();
    public DbSet<CalendarEvent> Events => Set<CalendarEvent>();
    public DbSet<Assignment> Assignments => Set<Assignment>();
    public DbSet<MainTask> MainTasks => Set<MainTask>();
    public DbSet<FoundItem> FoundItems => Set<FoundItem>();
    public DbSet<PersonalReminder> PersonalReminders => Set<PersonalReminder>();
    public DbSet<BulletinPost> Posts => Set<BulletinPost>();
    public DbSet<ClubChatMessage> Chat => Set<ClubChatMessage>();
    public DbSet<ClubPushSubscription> PushSubscriptions => Set<ClubPushSubscription>();
    public DbSet<EventRsvp> EventRsvps => Set<EventRsvp>();
    public DbSet<EventPrep> EventPreps => Set<EventPrep>();
    public DbSet<EventTimeSuggestion> EventTimeSuggestions => Set<EventTimeSuggestion>();
    public DbSet<EventTimeVote> EventTimeVotes => Set<EventTimeVote>();
    public DbSet<StorageLocation> StorageLocations => Set<StorageLocation>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<Idea> Ideas => Set<Idea>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Group>().HasIndex(g => g.Slug).IsUnique();
    }
}

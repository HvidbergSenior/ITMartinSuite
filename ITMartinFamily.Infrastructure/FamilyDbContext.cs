using ITMartinFamily.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ITMartinFamily.Infrastructure;

public sealed class FamilyDbContext(DbContextOptions<FamilyDbContext> options) : DbContext(options)
{
    public DbSet<DailyTask>        Tasks             => Set<DailyTask>();
    public DbSet<Family>           Families          => Set<Family>();
    public DbSet<FamilyMember>     Members           => Set<FamilyMember>();
    public DbSet<FamilySession>    Sessions          => Set<FamilySession>();
    public DbSet<ChatMessage>      Chat              => Set<ChatMessage>();
    public DbSet<PushSubscription> PushSubscriptions => Set<PushSubscription>();
    public DbSet<PersonalReminder> Reminders         => Set<PersonalReminder>();
    public DbSet<FamilyStoredItem> StoredItems       => Set<FamilyStoredItem>();
}

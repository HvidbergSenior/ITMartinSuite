using ITMartinTestHub.Server.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ITMartinTestHub.Server.Data;

public sealed class TestHubDbContext : DbContext
{
    public TestHubDbContext(DbContextOptions<TestHubDbContext> options) : base(options) { }

    public DbSet<AppEntry>       Apps        => Set<AppEntry>();
    public DbSet<TestStep>       Steps       => Set<TestStep>();
    public DbSet<Tester>         Testers     => Set<Tester>();
    public DbSet<TestRound>      Rounds      => Set<TestRound>();
    public DbSet<TestAssignment> Assignments => Set<TestAssignment>();
    public DbSet<StepResult>     Results     => Set<StepResult>();
    public DbSet<Feedback>       Feedbacks   => Set<Feedback>();
}

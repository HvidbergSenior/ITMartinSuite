using FluentAssertions;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using ITMartin.Media.Infrastructure.Persistence;
using ITMartin.Media.Infrastructure.Persistence.Entities;
using ITMartin.Media.Infrastructure.Persistence.Stores;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ITMartinFileSorter.Tests.Services;

// Requested 2026-09-07 ("And i need alarms.") after a real QuickSort run
// (RicoAC) sat "Running" but completely stuck on Manifest1BuildWorkflowStep
// for ~10 hours with nothing surfacing it - WorkflowExecutor only alerts on
// an actual thrown exception, and a genuine hang throws nothing. These cover
// the per-tick stall-detection logic directly (CheckOnceAsync), not the
// BackgroundService's real PeriodicTimer loop.
[TestFixture]
public class WorkflowStallWatchdogHostedServiceTests
{
    private static IDbContextFactory<MediaDbContext> CreateDbFactory(out MediaDbContext seedContext)
    {
        var options = new DbContextOptionsBuilder<MediaDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        seedContext = new MediaDbContext(options);
        return new TestDbContextFactory(options);
    }

    private static WorkflowStallWatchdogHostedService CreateService(
        IDbContextFactory<MediaDbContext> dbFactory,
        Mock<IWorkflowAlertNotifier> alertNotifier)
    {
        var services = new ServiceCollection();
        services.AddScoped<IWorkflowAlertNotifier>(_ => alertNotifier.Object);
        var provider = services.BuildServiceProvider();

        return new WorkflowStallWatchdogHostedService(
            dbFactory,
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<WorkflowStallWatchdogHostedService>.Instance);
    }

    [Test]
    public async Task Alerts_when_a_running_workflow_has_not_progressed_past_the_stall_threshold()
    {
        var db = CreateDbFactory(out var seed);
        await using var seedDisposable = seed;
        var workflowId = Guid.NewGuid();
        seed.WorkflowInstances.Add(new WorkflowInstanceEntity
        {
            WorkflowId = workflowId,
            WorkflowName = "QuickSortWorkflow",
            Status = "Running",
            CurrentStep = "Manifest",
            StartedAtUtc = DateTime.UtcNow - TimeSpan.FromHours(10),
            UpdatedAtUtc = DateTime.UtcNow - TimeSpan.FromHours(9),
        });
        await seed.SaveChangesAsync();

        var alertNotifier = new Mock<IWorkflowAlertNotifier>();
        var service = CreateService(db, alertNotifier);

        await service.CheckOnceAsync(CancellationToken.None);

        alertNotifier.Verify(a => a.NotifyStalledAsync(
            workflowId,
            "QuickSortWorkflow",
            "Manifest",
            It.Is<TimeSpan>(t => t >= TimeSpan.FromHours(9)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Does_not_alert_for_a_running_workflow_updated_recently()
    {
        var db = CreateDbFactory(out var seed);
        await using var seedDisposable = seed;
        seed.WorkflowInstances.Add(new WorkflowInstanceEntity
        {
            WorkflowId = Guid.NewGuid(),
            WorkflowName = "QuickSortWorkflow",
            Status = "Running",
            CurrentStep = "MediaRules",
            StartedAtUtc = DateTime.UtcNow - TimeSpan.FromMinutes(5),
            UpdatedAtUtc = DateTime.UtcNow - TimeSpan.FromMinutes(1),
        });
        await seed.SaveChangesAsync();

        var alertNotifier = new Mock<IWorkflowAlertNotifier>();
        var service = CreateService(db, alertNotifier);

        await service.CheckOnceAsync(CancellationToken.None);

        alertNotifier.Verify(a => a.NotifyStalledAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Test]
    public async Task Does_not_alert_twice_for_the_same_stalled_workflow()
    {
        var db = CreateDbFactory(out var seed);
        await using var seedDisposable = seed;
        var workflowId = Guid.NewGuid();
        seed.WorkflowInstances.Add(new WorkflowInstanceEntity
        {
            WorkflowId = workflowId,
            WorkflowName = "QuickSortWorkflow",
            Status = "Running",
            CurrentStep = "Manifest",
            StartedAtUtc = DateTime.UtcNow - TimeSpan.FromHours(10),
            UpdatedAtUtc = DateTime.UtcNow - TimeSpan.FromHours(9),
        });
        seed.WorkflowAlerts.Add(new WorkflowAlertEntity
        {
            WorkflowId = workflowId.ToString(),
            WorkflowName = "QuickSortWorkflow",
            Kind = "Stalled",
            Message = "already alerted once",
        });
        await seed.SaveChangesAsync();

        var alertNotifier = new Mock<IWorkflowAlertNotifier>();
        var service = CreateService(db, alertNotifier);

        await service.CheckOnceAsync(CancellationToken.None);

        alertNotifier.Verify(a => a.NotifyStalledAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Test]
    public async Task Does_not_alert_for_a_completed_workflow_even_if_long_since_updated()
    {
        var db = CreateDbFactory(out var seed);
        await using var seedDisposable = seed;
        seed.WorkflowInstances.Add(new WorkflowInstanceEntity
        {
            WorkflowId = Guid.NewGuid(),
            WorkflowName = "QuickSortWorkflow",
            Status = "Completed",
            CurrentStep = "Export",
            StartedAtUtc = DateTime.UtcNow - TimeSpan.FromDays(2),
            UpdatedAtUtc = DateTime.UtcNow - TimeSpan.FromDays(1),
            CompletedAtUtc = DateTime.UtcNow - TimeSpan.FromDays(1),
        });
        await seed.SaveChangesAsync();

        var alertNotifier = new Mock<IWorkflowAlertNotifier>();
        var service = CreateService(db, alertNotifier);

        await service.CheckOnceAsync(CancellationToken.None);

        alertNotifier.Verify(a => a.NotifyStalledAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private sealed class TestDbContextFactory(DbContextOptions<MediaDbContext> options)
        : IDbContextFactory<MediaDbContext>
    {
        public MediaDbContext CreateDbContext() => new(options);
    }
}

using FluentAssertions;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using ITMartin.Media.Infrastructure.Persistence;
using ITMartin.Media.Infrastructure.Persistence.Entities;
using ITMartin.Media.Infrastructure.Persistence.Stores;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ITMartinFileSorter.Tests.Services;

// Requested 2026-09-08 ("build the auto-finish hosted service") after the
// finish-library/push-to-nas/wire-gallery chain turned out to only ever run
// via a human (or Claude) manually calling three separate debug endpoints
// once QuickSort itself finished - nothing watched for completion and
// continued the chain on its own. These cover the per-tick logic directly
// (CheckOnceAsync), same convention as WorkflowStallWatchdogHostedServiceTests.
[TestFixture]
public class QuickSortAutoFinishHostedServiceTests
{
    private string _libraryRoot = null!;

    [SetUp]
    public void SetUp()
    {
        _libraryRoot = Path.Combine(Path.GetTempPath(), "autofinish-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(_libraryRoot);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_libraryRoot))
        {
            Directory.Delete(_libraryRoot, recursive: true);
        }
    }

    private static IDbContextFactory<MediaDbContext> CreateDbFactory(out MediaDbContext seedContext)
    {
        var options = new DbContextOptionsBuilder<MediaDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        seedContext = new MediaDbContext(options);
        return new TestDbContextFactory(options);
    }

    private IConfiguration CreateConfig(bool withClientSlug = true) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(withClientSlug
                ? new Dictionary<string, string?>
                {
                    ["MediaSettings:ClientSlug"] = "ToshibaTest",
                    ["MediaSettings:LibraryRoot"] = _libraryRoot,
                }
                : new Dictionary<string, string?>())
            .Build();

    private QuickSortAutoFinishHostedService CreateService(
        IDbContextFactory<MediaDbContext> dbFactory,
        IConfiguration config,
        Mock<ILibraryFinishingService> finishing,
        Mock<INasDeliveryService> nasDelivery,
        Mock<IWorkflowAlertNotifier> alertNotifier)
    {
        var services = new ServiceCollection();
        services.AddScoped<ILibraryFinishingService>(_ => finishing.Object);
        services.AddScoped<INasDeliveryService>(_ => nasDelivery.Object);
        services.AddScoped<IWorkflowAlertNotifier>(_ => alertNotifier.Object);
        var provider = services.BuildServiceProvider();

        return new QuickSortAutoFinishHostedService(
            dbFactory,
            provider.GetRequiredService<IServiceScopeFactory>(),
            config,
            NullLogger<QuickSortAutoFinishHostedService>.Instance);
    }

    private static (Mock<ILibraryFinishingService>, Mock<INasDeliveryService>, Mock<IWorkflowAlertNotifier>) CreateHappyPathMocks()
    {
        var finishing = new Mock<ILibraryFinishingService>();
        finishing
            .Setup(f => f.RunAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string path, CancellationToken _) => new LibraryFinishingReport { LibraryPath = path, FinishedAtUtc = DateTime.UtcNow });

        var nasDelivery = new Mock<INasDeliveryService>();
        nasDelivery
            .Setup(n => n.PushToNasAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NasPushResult { Success = true, RemotePath = "/volume1/docker/filesorter/library/toshibatest" });
        nasDelivery
            .Setup(n => n.WireGalleryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GalleryWireResult { Success = true, AssignedIndex = 3 });

        return (finishing, nasDelivery, new Mock<IWorkflowAlertNotifier>());
    }

    [Test]
    public async Task Runs_the_full_chain_when_a_quicksort_run_just_completed()
    {
        var db = CreateDbFactory(out var seed);
        await using var seedDisposable = seed;
        var workflowId = Guid.NewGuid();
        seed.WorkflowInstances.Add(new WorkflowInstanceEntity
        {
            WorkflowId = workflowId,
            WorkflowName = "QuickSortWorkflow",
            Status = "Completed",
            StartedAtUtc = DateTime.UtcNow - TimeSpan.FromHours(4),
            UpdatedAtUtc = DateTime.UtcNow,
            CompletedAtUtc = DateTime.UtcNow,
        });
        await seed.SaveChangesAsync();

        var (finishing, nasDelivery, alertNotifier) = CreateHappyPathMocks();
        var service = CreateService(db, CreateConfig(), finishing, nasDelivery, alertNotifier);

        await service.CheckOnceAsync(CancellationToken.None);

        finishing.Verify(f => f.RunAsync(_libraryRoot, It.IsAny<CancellationToken>()), Times.Once);
        nasDelivery.Verify(n => n.PushToNasAsync(_libraryRoot, "toshibatest", It.IsAny<CancellationToken>()), Times.Once);
        nasDelivery.Verify(n => n.WireGalleryAsync("toshibatest", "ToshibaTest", It.Is<string>(p => p.StartsWith("ToshibaTest")), It.IsAny<CancellationToken>()), Times.Once);
        alertNotifier.Verify(a => a.NotifyDeliveredAsync(workflowId, "QuickSortWorkflow", "toshibatest", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Writes_a_marker_file_recording_the_delivered_workflow_id()
    {
        var db = CreateDbFactory(out var seed);
        await using var seedDisposable = seed;
        var workflowId = Guid.NewGuid();
        seed.WorkflowInstances.Add(new WorkflowInstanceEntity
        {
            WorkflowId = workflowId,
            WorkflowName = "QuickSortWorkflow",
            Status = "Completed",
            StartedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
        });
        await seed.SaveChangesAsync();

        var (finishing, nasDelivery, alertNotifier) = CreateHappyPathMocks();
        var service = CreateService(db, CreateConfig(), finishing, nasDelivery, alertNotifier);

        await service.CheckOnceAsync(CancellationToken.None);

        var markerPath = Path.Combine(_libraryRoot, ".autofinished");
        File.Exists(markerPath).Should().BeTrue();
        (await File.ReadAllTextAsync(markerPath)).Trim().Should().Be(workflowId.ToString());
    }

    [Test]
    public async Task Does_not_redeliver_a_run_already_marked_as_finished()
    {
        var db = CreateDbFactory(out var seed);
        await using var seedDisposable = seed;
        var workflowId = Guid.NewGuid();
        seed.WorkflowInstances.Add(new WorkflowInstanceEntity
        {
            WorkflowId = workflowId,
            WorkflowName = "QuickSortWorkflow",
            Status = "Completed",
            StartedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
        });
        await seed.SaveChangesAsync();
        await File.WriteAllTextAsync(Path.Combine(_libraryRoot, ".autofinished"), workflowId.ToString());

        var (finishing, nasDelivery, alertNotifier) = CreateHappyPathMocks();
        var service = CreateService(db, CreateConfig(), finishing, nasDelivery, alertNotifier);

        await service.CheckOnceAsync(CancellationToken.None);

        finishing.Verify(f => f.RunAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Redelivers_a_new_run_even_when_an_older_run_was_already_marked_finished()
    {
        var db = CreateDbFactory(out var seed);
        await using var seedDisposable = seed;
        var oldWorkflowId = Guid.NewGuid();
        var newWorkflowId = Guid.NewGuid();
        seed.WorkflowInstances.Add(new WorkflowInstanceEntity
        {
            WorkflowId = newWorkflowId,
            WorkflowName = "QuickSortWorkflow",
            Status = "Completed",
            StartedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
        });
        await seed.SaveChangesAsync();
        await File.WriteAllTextAsync(Path.Combine(_libraryRoot, ".autofinished"), oldWorkflowId.ToString());

        var (finishing, nasDelivery, alertNotifier) = CreateHappyPathMocks();
        var service = CreateService(db, CreateConfig(), finishing, nasDelivery, alertNotifier);

        await service.CheckOnceAsync(CancellationToken.None);

        finishing.Verify(f => f.RunAsync(_libraryRoot, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Does_nothing_while_the_run_is_still_going()
    {
        var db = CreateDbFactory(out var seed);
        await using var seedDisposable = seed;
        seed.WorkflowInstances.Add(new WorkflowInstanceEntity
        {
            WorkflowId = Guid.NewGuid(),
            WorkflowName = "QuickSortWorkflow",
            Status = "Running",
            StartedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
        });
        await seed.SaveChangesAsync();

        var (finishing, nasDelivery, alertNotifier) = CreateHappyPathMocks();
        var service = CreateService(db, CreateConfig(), finishing, nasDelivery, alertNotifier);

        await service.CheckOnceAsync(CancellationToken.None);

        finishing.Verify(f => f.RunAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Does_nothing_when_this_process_has_no_client_slug_configured()
    {
        var db = CreateDbFactory(out var seed);
        await using var seedDisposable = seed;
        seed.WorkflowInstances.Add(new WorkflowInstanceEntity
        {
            WorkflowId = Guid.NewGuid(),
            WorkflowName = "QuickSortWorkflow",
            Status = "Completed",
            StartedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
        });
        await seed.SaveChangesAsync();

        var (finishing, nasDelivery, alertNotifier) = CreateHappyPathMocks();
        var service = CreateService(db, CreateConfig(withClientSlug: false), finishing, nasDelivery, alertNotifier);

        await service.CheckOnceAsync(CancellationToken.None);

        finishing.Verify(f => f.RunAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Alerts_failure_and_does_not_write_a_marker_when_push_to_nas_fails()
    {
        var db = CreateDbFactory(out var seed);
        await using var seedDisposable = seed;
        var workflowId = Guid.NewGuid();
        seed.WorkflowInstances.Add(new WorkflowInstanceEntity
        {
            WorkflowId = workflowId,
            WorkflowName = "QuickSortWorkflow",
            Status = "Completed",
            StartedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
        });
        await seed.SaveChangesAsync();

        var finishing = new Mock<ILibraryFinishingService>();
        finishing
            .Setup(f => f.RunAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string path, CancellationToken _) => new LibraryFinishingReport { LibraryPath = path, FinishedAtUtc = DateTime.UtcNow });

        var nasDelivery = new Mock<INasDeliveryService>();
        nasDelivery
            .Setup(n => n.PushToNasAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NasPushResult { Success = false, Error = "ssh timed out" });

        var alertNotifier = new Mock<IWorkflowAlertNotifier>();
        var service = CreateService(db, CreateConfig(), finishing, nasDelivery, alertNotifier);

        await service.CheckOnceAsync(CancellationToken.None);

        nasDelivery.Verify(n => n.WireGalleryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        alertNotifier.Verify(a => a.NotifyFailedAsync(workflowId, "QuickSortWorkflow", It.Is<string>(m => m.Contains("push-to-nas")), It.IsAny<CancellationToken>()), Times.Once);
        File.Exists(Path.Combine(_libraryRoot, ".autofinished")).Should().BeFalse();
    }

    // A failed chain deliberately writes no marker file, so nothing else stops
    // the next tick from trying again - and every attempt re-runs a full
    // tar+scp of the entire library to the NAS. Before the attempt cap, a
    // persistent failure re-pushed the whole library every 2 minutes for as
    // long as the process lived.
    [Test]
    public async Task Stops_retrying_the_chain_after_repeated_failures()
    {
        var db = CreateDbFactory(out var seed);
        await using var seedDisposable = seed;
        seed.WorkflowInstances.Add(new WorkflowInstanceEntity
        {
            WorkflowId = Guid.NewGuid(),
            WorkflowName = "QuickSortWorkflow",
            Status = "Completed",
            StartedAtUtc = DateTime.UtcNow - TimeSpan.FromHours(4),
            UpdatedAtUtc = DateTime.UtcNow,
            CompletedAtUtc = DateTime.UtcNow,
        });
        await seed.SaveChangesAsync();

        var (finishing, nasDelivery, alertNotifier) = CreateHappyPathMocks();
        nasDelivery
            .Setup(n => n.WireGalleryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GalleryWireResult { Success = false, Error = "gallery container unreachable" });

        var service = CreateService(db, CreateConfig(), finishing, nasDelivery, alertNotifier);

        for (var i = 0; i < 10; i++)
        {
            await service.CheckOnceAsync(CancellationToken.None);
        }

        nasDelivery.Verify(
            n => n.PushToNasAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Exactly(3));
        File.Exists(Path.Combine(_libraryRoot, ".autofinished")).Should().BeFalse();
    }

    [Test]
    public async Task A_recovered_run_that_succeeds_still_delivers_after_earlier_failures()
    {
        var db = CreateDbFactory(out var seed);
        await using var seedDisposable = seed;
        seed.WorkflowInstances.Add(new WorkflowInstanceEntity
        {
            WorkflowId = Guid.NewGuid(),
            WorkflowName = "QuickSortWorkflow",
            Status = "Completed",
            StartedAtUtc = DateTime.UtcNow - TimeSpan.FromHours(4),
            UpdatedAtUtc = DateTime.UtcNow,
            CompletedAtUtc = DateTime.UtcNow,
        });
        await seed.SaveChangesAsync();

        var (finishing, nasDelivery, alertNotifier) = CreateHappyPathMocks();
        nasDelivery
            .Setup(n => n.WireGalleryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GalleryWireResult { Success = false, Error = "transient" });

        var service = CreateService(db, CreateConfig(), finishing, nasDelivery, alertNotifier);
        await service.CheckOnceAsync(CancellationToken.None);

        nasDelivery
            .Setup(n => n.WireGalleryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GalleryWireResult { Success = true, AssignedIndex = 1 });

        await service.CheckOnceAsync(CancellationToken.None);

        File.Exists(Path.Combine(_libraryRoot, ".autofinished")).Should().BeTrue();
    }

    private sealed class TestDbContextFactory(DbContextOptions<MediaDbContext> options)
        : IDbContextFactory<MediaDbContext>
    {
        public MediaDbContext CreateDbContext() => new(options);
    }
}

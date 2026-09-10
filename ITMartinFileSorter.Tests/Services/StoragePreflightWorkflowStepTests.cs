using FluentAssertions;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Application.Pipelines.QuickSort.Steps;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ITMartinFileSorter.Tests.Services;

// This step is what stands between a bad storage situation and a ruined
// delivery, and on 2026-09-10 it shipped two bugs in a single day - both of
// the same kind, a check that silently measured something other than what it
// claimed:
//
//   1. It demanded a flat 5 GB free regardless of the source. That passes a
//      276 GB job aimed at a disk with 104 GB left.
//   2. It measured free space with DriveInfo(Path.GetPathRoot(path)), which on
//      Linux is always "/" - so it reported the root filesystem no matter
//      which path it was asked about, and refused a run onto a freshly
//      mounted 932 GB drive.
//
// The second is the more instructive: it blocked a GOOD run, which is the
// harmless direction. The same call decides whether there IS room.
[TestFixture]
public class StoragePreflightWorkflowStepTests
{
    private string _root = string.Empty;

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), "preflight-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private string MakeSource(int files = 3)
    {
        var dir = Path.Combine(_root, "kilde");
        Directory.CreateDirectory(dir);
        for (var i = 0; i < files; i++)
            File.WriteAllText(Path.Combine(dir, $"foto{i}.jpg"), new string('x', 1024));
        return dir;
    }

    private static StoragePreflightWorkflowStep StepFor(string libraryRoot)
    {
        var provider = new Mock<ILibraryPathProvider>();
        provider.SetupGet(p => p.LibraryRoot).Returns(libraryRoot);

        return new StoragePreflightWorkflowStep(
            provider.Object,
            NullLogger<StoragePreflightWorkflowStep>.Instance);
    }

    private static Task RunAsync(StoragePreflightWorkflowStep step, string source, string output) =>
        step.ExecuteAsync(new WorkflowExecutionContext<QuickSortWorkflowState>
        {
            WorkflowId = Guid.NewGuid(),
            WorkflowName = "test",
            State = new QuickSortWorkflowState { RootPath = source, OutputPath = output },
        });

    [Test]
    public async Task Passes_for_a_real_source_and_a_writable_output()
    {
        var source = MakeSource();
        var output = Path.Combine(_root, "ud");

        await RunAsync(StepFor(output), source, output);
    }

    [Test]
    public async Task Creates_the_output_directory_rather_than_demanding_it_exists()
    {
        var source = MakeSource();
        var output = Path.Combine(_root, "findes-ikke-endnu");

        await RunAsync(StepFor(output), source, output);

        // A first run for a new client has no output folder yet - that is
        // normal, not a reason to refuse.
        Directory.Exists(output).Should().BeTrue();
    }

    [Test]
    public async Task Refuses_a_source_that_does_not_exist()
    {
        var output = Path.Combine(_root, "ud");
        var step = StepFor(output);

        var act = async () => await RunAsync(step, Path.Combine(_root, "vaek"), output);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*does not exist*");
    }

    [Test]
    public async Task Refuses_an_EMPTY_source()
    {
        // The quiet killer: a drive that remounted, or a mount point left
        // unmounted so the empty folder underneath shows through. Everything
        // reads and writes perfectly and the run delivers nothing.
        var source = Path.Combine(_root, "tom-kilde");
        Directory.CreateDirectory(source);
        var output = Path.Combine(_root, "ud");
        var step = StepFor(output);

        var act = async () => await RunAsync(step, source, output);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*EMPTY*");
    }

    [Test]
    public async Task Refuses_when_no_source_path_was_given()
    {
        var output = Path.Combine(_root, "ud");
        var step = StepFor(output);

        var act = async () => await RunAsync(step, string.Empty, output);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*No source path*");
    }

    [Test]
    public async Task Falls_back_to_the_library_root_when_no_output_path_is_set()
    {
        // Must agree with ExportWorkflowExecutionStep and the add-on chain, or
        // the preflight checks a path the run never writes to.
        var source = MakeSource();
        var libraryRoot = Path.Combine(_root, "bibliotek");

        await RunAsync(StepFor(libraryRoot), source, string.Empty);

        Directory.Exists(libraryRoot).Should().BeTrue();
    }

    [Test]
    public async Task Measures_free_space_on_the_filesystem_of_the_path_itself()
    {
        // The GetPathRoot bug: on Linux every absolute path roots to "/", so
        // the check reported the root filesystem's free space for any output
        // path at all. A temp directory is on a real filesystem with real free
        // space, so the only way this passes is if the measurement resolved a
        // sane figure for THIS path rather than throwing or reading zero.
        var source = MakeSource();
        var output = Path.Combine(_root, "ud");

        await RunAsync(StepFor(output), source, output);

        new DriveInfo(_root).AvailableFreeSpace.Should().BeGreaterThan(0);
    }
}

using FluentAssertions;
using ITMartin.Media.Application.Pipelines.Package2.Services;
using ITMartin.Media.Application.Pipelines.Package4.Orchestration;
using ITMartin.Media.Application.Pipelines.Package4.Services;
using ITMartin.Media.Contracts.Contracts.Runtime.Requests.Package4;
using Microsoft.Extensions.Logging.Abstractions;

namespace ITMartinFileSorter.Tests.Package1Tests;

// Covers the warning-only Package1-required validation added 2026-08-25:
// Package4 still works on a raw, never-sorted clip folder (the documented
// Package4 Studio one-off-clip workflow), but the orchestrator's result now
// flags whether the source had actually been through Package1 first
// (manifest.json present), so callers can surface a warning rather than
// silently treating both paths as equivalent.
[TestFixture]
public class Package4WorkflowOrchestratorTests
{
    private string _root = "";
    private Package4WorkflowOrchestrator _orchestrator = null!;

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), "Package4OrchestratorTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        _orchestrator = new Package4WorkflowOrchestrator(
            new Package4WorkflowFactory(),
            new Package1ManifestLoader(),
            NullLogger<Package4WorkflowOrchestrator>.Instance);
    }

    [TearDown]
    public void TearDown()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* best effort */ }
    }

    private StartPackage4Request MakeRequest() => new()
    {
        SourceLibraryPath = _root,
        WorkingDirectory = Path.Combine(_root, ".package4"),
    };

    [Test]
    public async Task HasRunThroughPackage1_is_false_when_no_manifest_json_present()
    {
        var result = await _orchestrator.StartAsync(MakeRequest(), CancellationToken.None);
        result.HasRunThroughPackage1.Should().BeFalse();
    }

    [Test]
    public async Task HasRunThroughPackage1_is_true_when_manifest_json_is_present()
    {
        File.WriteAllText(Path.Combine(_root, "manifest.json"), """{"WorkflowId":"00000000-0000-0000-0000-000000000000","RootPath":"","CreatedAtUtc":"2026-01-01T00:00:00Z","MediaFiles":[],"FileCount":0}""");

        var result = await _orchestrator.StartAsync(MakeRequest(), CancellationToken.None);
        result.HasRunThroughPackage1.Should().BeTrue();
    }

    [Test]
    public async Task Raw_folder_scan_still_works_without_a_manifest_the_documented_one_off_clip_workflow()
    {
        // Still the supported path for a folder that's never been through
        // Package1 - the warning is informational, not a hard block.
        var result = await _orchestrator.StartAsync(MakeRequest(), CancellationToken.None);
        result.State.Should().NotBeNull();
    }
}

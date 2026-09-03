using FluentAssertions;
using ITMartin.Media.Application.Pipelines.QuickSort.Steps;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using Microsoft.Extensions.Logging.Abstractions;

namespace ITMartinFileSorter.Tests.QuickSortTests;

// QuickSort must only ever run against genuine raw source material - a folder
// that's actually a copy of a previously-sorted library (e.g. pulled back
// off the NAS/an external HD) needs its prior run's generated artifacts
// stripped first, or QuickSort would ingest _Galleri thumbnails/SmartFolders
// copies as if they were new content. See CleanStartWorkflowStep.
[TestFixture]
public class CleanStartWorkflowStepTests
{
    private string _root = "";

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), "QuickSortCleanStartTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    [TearDown]
    public void TearDown()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* best effort */ }
    }

    private async Task RunAsync()
    {
        var state = new QuickSortWorkflowState { RootPath = _root };
        var step = new CleanStartWorkflowStep(NullLogger<CleanStartWorkflowStep>.Instance);
        var context = new WorkflowExecutionContext<QuickSortWorkflowState>
        {
            WorkflowName = "QuickSortWorkflow",
            State = state,
        };

        await step.ExecuteAsync(context);
    }

    [Test]
    public async Task Removes_generated_folders_and_files_from_a_prior_run()
    {
        Directory.CreateDirectory(Path.Combine(_root, "_Galleri"));
        Directory.CreateDirectory(Path.Combine(_root, "SmartFolders"));
        Directory.CreateDirectory(Path.Combine(_root, ".package1"));
        File.WriteAllText(Path.Combine(_root, "manifest.json"), "{}");
        File.WriteAllText(Path.Combine(_root, "collections.json"), "{}");
        File.WriteAllText(Path.Combine(_root, "filestatus.json"), "{}");
        File.WriteAllText(Path.Combine(_root, "index.html"), "<html></html>");

        await RunAsync();

        Directory.Exists(Path.Combine(_root, "_Galleri")).Should().BeFalse();
        Directory.Exists(Path.Combine(_root, "SmartFolders")).Should().BeFalse();
        Directory.Exists(Path.Combine(_root, ".package1")).Should().BeFalse();
        File.Exists(Path.Combine(_root, "manifest.json")).Should().BeFalse();
        File.Exists(Path.Combine(_root, "collections.json")).Should().BeFalse();
        File.Exists(Path.Combine(_root, "filestatus.json")).Should().BeFalse();
        File.Exists(Path.Combine(_root, "index.html")).Should().BeFalse();
    }

    [Test]
    public async Task Leaves_real_content_folders_untouched()
    {
        Directory.CreateDirectory(Path.Combine(_root, "Billeder"));
        File.WriteAllBytes(Path.Combine(_root, "Billeder", "photo.jpg"), [0x01]);
        Directory.CreateDirectory(Path.Combine(_root, "_Galleri"));

        await RunAsync();

        Directory.Exists(Path.Combine(_root, "Billeder")).Should().BeTrue();
        File.Exists(Path.Combine(_root, "Billeder", "photo.jpg")).Should().BeTrue();
        Directory.Exists(Path.Combine(_root, "_Galleri")).Should().BeFalse();
    }

    [Test]
    public async Task Is_a_no_op_on_genuinely_raw_input_with_nothing_generated()
    {
        Directory.CreateDirectory(Path.Combine(_root, "DCIM"));
        File.WriteAllBytes(Path.Combine(_root, "DCIM", "IMG_0001.jpg"), [0x01]);

        await RunAsync();

        Directory.Exists(Path.Combine(_root, "DCIM")).Should().BeTrue();
        File.Exists(Path.Combine(_root, "DCIM", "IMG_0001.jpg")).Should().BeTrue();
    }
}

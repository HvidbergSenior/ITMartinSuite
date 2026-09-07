using System.IO.Compression;
using FluentAssertions;
using ITMartin.Media.Application.Pipelines.QuickSort.Steps;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using Microsoft.Extensions.Logging.Abstractions;

namespace ITMartinFileSorter.Tests.QuickSortTests;

// Covers the gap found 2026-09-07: no zip-handling existed anywhere in
// QuickSort - a .zip in a raw source folder (routine on a whole-drive backup
// dump, e.g. iCloud/Dropbox export zips) was carried through FileDiscovery
// as one opaque "Other" file, its contents never sorted. This step extracts
// zips before FileDiscovery ever scans the tree.
[TestFixture]
public class ZipExtractionWorkflowStepTests
{
    private string _root = "";

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), "QuickSortZipExtractionTests_" + Guid.NewGuid().ToString("N"));
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
        var step = new ZipExtractionWorkflowStep(NullLogger<ZipExtractionWorkflowStep>.Instance);
        var context = new WorkflowExecutionContext<QuickSortWorkflowState>
        {
            WorkflowName = "QuickSortWorkflow",
            State = state,
        };

        await step.ExecuteAsync(context);
    }

    private static void CreateZip(string zipPath, params (string Name, string Content)[] entries)
    {
        using var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create);
        foreach (var (name, content) in entries)
        {
            var entry = zip.CreateEntry(name);
            using var writer = new StreamWriter(entry.Open());
            writer.Write(content);
        }
    }

    [Test]
    public async Task Extracts_a_zip_into_a_sibling_extracted_folder_and_archives_the_original()
    {
        var zipPath = Path.Combine(_root, "iCloud-fotos (7).zip");
        CreateZip(zipPath, ("IMG_0001.jpg", "fake-photo-bytes"));

        await RunAsync();

        var extractedDir = Path.Combine(_root, "iCloud-fotos (7)_extracted");
        Directory.Exists(extractedDir).Should().BeTrue();
        File.Exists(Path.Combine(extractedDir, "IMG_0001.jpg")).Should().BeTrue();

        File.Exists(zipPath).Should().BeFalse("the original zip should be archived away, not left for FileDiscovery to see");
        File.Exists(Path.Combine(_root, ".zip-source", "iCloud-fotos (7).zip")).Should().BeTrue();
    }

    [Test]
    public async Task Finds_zips_nested_in_subfolders()
    {
        var subfolder = Path.Combine(_root, "Backup", "Dropbox1");
        Directory.CreateDirectory(subfolder);
        var zipPath = Path.Combine(subfolder, "export.zip");
        CreateZip(zipPath, ("photo.jpg", "bytes"));

        await RunAsync();

        Directory.Exists(Path.Combine(subfolder, "export_extracted")).Should().BeTrue();
        File.Exists(Path.Combine(subfolder, "export_extracted", "photo.jpg")).Should().BeTrue();
    }

    [Test]
    public async Task Skips_extraction_but_still_archives_when_the_extracted_folder_already_exists()
    {
        var zipPath = Path.Combine(_root, "old-export.zip");
        CreateZip(zipPath, ("photo.jpg", "bytes"));

        var alreadyExtracted = Path.Combine(_root, "old-export_extracted");
        Directory.CreateDirectory(alreadyExtracted);
        File.WriteAllText(Path.Combine(alreadyExtracted, "manually-placed.jpg"), "hand-extracted earlier");

        await RunAsync();

        File.Exists(Path.Combine(alreadyExtracted, "manually-placed.jpg")).Should().BeTrue("existing extraction should not be overwritten");
        File.Exists(zipPath).Should().BeFalse();
        File.Exists(Path.Combine(_root, ".zip-source", "old-export.zip")).Should().BeTrue();
    }

    [Test]
    public async Task Leaves_a_corrupt_zip_in_place_instead_of_archiving_it()
    {
        var zipPath = Path.Combine(_root, "corrupt.zip");
        File.WriteAllBytes(zipPath, [0x50, 0x4B, 0x00, 0x00]); // PK header but garbage after

        await RunAsync();

        File.Exists(zipPath).Should().BeTrue("a failed extraction should stay visible for manual review, not vanish into .zip-source");
        Directory.Exists(Path.Combine(_root, ".zip-source")).Should().BeFalse();
    }

    [Test]
    public async Task Is_a_no_op_when_there_are_no_zip_files()
    {
        Directory.CreateDirectory(Path.Combine(_root, "DCIM"));
        File.WriteAllBytes(Path.Combine(_root, "DCIM", "IMG_0001.jpg"), [0x01]);

        await RunAsync();

        Directory.Exists(Path.Combine(_root, ".zip-source")).Should().BeFalse();
        File.Exists(Path.Combine(_root, "DCIM", "IMG_0001.jpg")).Should().BeTrue();
    }
}

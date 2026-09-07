using FluentAssertions;
using ITMartin.Media.Application.Interfaces;
using ITMartin.Media.Application.Pipelines.QuickSort.Steps;
using ITMartin.Media.Contracts.Contracts.Runtime.Enums;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Persistence;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ITMartinFileSorter.Tests.QuickSortTests;

// Covers the gap found 2026-09-07: no zero-byte check existed anywhere in
// QuickSort. A 0-byte file (interrupted copy, failed download, truncated
// backup) carried no usable content but still flowed through as a real
// MediaFile under any recognized extension.
[TestFixture]
public class FileDiscoveryWorkflowStepTests
{
    private string _root = "";

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), "QuickSortFileDiscoveryTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    [TearDown]
    public void TearDown()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* best effort */ }
    }

    private async Task<QuickSortWorkflowState> RunAsync()
    {
        var scanner = new Mock<IFileScanner>();
        scanner
            .Setup(s => s.ScanAsync(_root, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories));

        var typeResolver = new Mock<IMediaTypeResolver>();
        typeResolver.Setup(r => r.Resolve(It.IsAny<string>())).Returns(MediaType.Image);

        var dateService = new Mock<IMediaDateService>();
        dateService
            .Setup(d => d.GetBestDate(It.IsAny<MediaDateRequest>()))
            .Returns(new MediaDateResult(DateTime.UtcNow, true, "test"));

        var instanceStore = new Mock<IWorkflowInstanceStore>();

        var step = new FileDiscoveryWorkflowStep(
            scanner.Object,
            typeResolver.Object,
            dateService.Object,
            instanceStore.Object,
            NullLogger<FileDiscoveryWorkflowStep>.Instance);

        var state = new QuickSortWorkflowState { RootPath = _root };
        var context = new WorkflowExecutionContext<QuickSortWorkflowState>
        {
            WorkflowName = "QuickSortWorkflow",
            State = state,
        };

        await step.ExecuteAsync(context);
        return state;
    }

    [Test]
    public async Task Skips_zero_byte_files()
    {
        File.WriteAllBytes(Path.Combine(_root, "empty.jpg"), []);
        File.WriteAllBytes(Path.Combine(_root, "real.jpg"), [0x01, 0x02, 0x03]);

        var state = await RunAsync();

        state.MediaFiles.Should().ContainSingle();
        state.MediaFiles.Single().FullPath.Should().Be(Path.Combine(_root, "real.jpg"));
    }

    [Test]
    public async Task Keeps_non_empty_files_of_any_size()
    {
        File.WriteAllBytes(Path.Combine(_root, "tiny.jpg"), [0x01]);

        var state = await RunAsync();

        state.MediaFiles.Should().ContainSingle();
        state.MediaFiles.Single().SizeBytes.Should().Be(1);
    }
}

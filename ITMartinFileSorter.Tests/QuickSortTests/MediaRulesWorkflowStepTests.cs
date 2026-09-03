using FluentAssertions;
using ITMartin.Media.Application.Pipelines.QuickSort.Steps;
using ITMartin.Media.Contracts.Contracts.Runtime.Enums;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ITMartinFileSorter.Tests.QuickSortTests;

// Covers the album-art-lands-in-Billeder issue found 2026-08-25 on mie's real
// library - cover.jpg/folder.jpg-style files sitting next to music got
// classified as ordinary photos. Requires both a standard filename AND an
// audio file in the same folder, so a real personal photo that happens to be
// named "cover.jpg" (e.g. a scanned book cover) isn't swept into Musik too.
[TestFixture]
public class MediaRulesWorkflowStepTests
{
    private string _root = "";

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), "QuickSortMediaRulesTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    [TearDown]
    public void TearDown()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* best effort */ }
    }

    private async Task<MediaFile> ClassifyAsync(string fileName, bool withAudioSibling)
    {
        var path = Path.Combine(_root, fileName);
        File.WriteAllBytes(path, [0x01, 0x02, 0x03]);
        if (withAudioSibling)
            File.WriteAllBytes(Path.Combine(_root, "track01.mp3"), [0x01]);

        var file = new MediaFile(path, DateTime.UtcNow, MediaType.Image, 3);
        var state = new QuickSortWorkflowState { MediaFiles = [file] };
        var step = new MediaRulesWorkflowStep(Mock.Of<IVideoMetadataService>(), Mock.Of<IConcurrentVideoDispatcher>(), NullLogger<MediaRulesWorkflowStep>.Instance);
        var context = new WorkflowExecutionContext<QuickSortWorkflowState> { WorkflowName = "QuickSortWorkflow", State = state };

        await step.ExecuteAsync(context);
        return file;
    }

    [Test]
    public async Task Cover_jpg_next_to_music_is_classified_as_AlbumArt()
    {
        var file = await ClassifyAsync("cover.jpg", withAudioSibling: true);
        file.SubCategory.Should().Be(MediaSubCategory.AlbumArt);
    }

    [Test]
    public async Task Folder_jpg_next_to_music_is_classified_as_AlbumArt()
    {
        var file = await ClassifyAsync("folder.jpg", withAudioSibling: true);
        file.SubCategory.Should().Be(MediaSubCategory.AlbumArt);
    }

    [Test]
    public async Task Cover_jpg_with_no_music_in_the_folder_is_not_AlbumArt()
    {
        var file = await ClassifyAsync("cover.jpg", withAudioSibling: false);
        file.SubCategory.Should().NotBe(MediaSubCategory.AlbumArt);
    }

    [Test]
    public async Task An_ordinary_photo_next_to_music_is_not_AlbumArt()
    {
        var file = await ClassifyAsync("IMG_1234.jpg", withAudioSibling: true);
        file.SubCategory.Should().NotBe(MediaSubCategory.AlbumArt);
    }
}

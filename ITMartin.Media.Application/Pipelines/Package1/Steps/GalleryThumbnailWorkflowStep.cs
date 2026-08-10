using ITMartin.Media.Application.Interfaces;
using ITMartin.Media.Application.Pipelines.Package1.Orchestration;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Pipelines.Package1.Steps;

// Runs last, against the final exported library (not source paths, unlike the
// old unused ThumbnailWorkflowStep) - a gallery is only actually browsable
// once every image and video has a thumbnails/ entry, so this closes the loop
// on the same run rather than needing a separate manual trigger afterward.
public sealed class GalleryThumbnailWorkflowStep
    : Package1WorkflowStepBase
{
    private readonly IGalleryThumbnailService _galleryThumbnailService;
    private readonly ILibraryPathProvider _libraryPathProvider;
    private readonly ILogger<GalleryThumbnailWorkflowStep> _logger;

    public GalleryThumbnailWorkflowStep(
        IGalleryThumbnailService galleryThumbnailService,
        ILibraryPathProvider libraryPathProvider,
        ILogger<GalleryThumbnailWorkflowStep> logger)
    {
        _galleryThumbnailService = galleryThumbnailService;
        _libraryPathProvider = libraryPathProvider;
        _logger = logger;
    }

    public override string Name => "GalleryThumbnails";

    public override async Task ExecuteAsync<TState>(
        WorkflowExecutionContext<TState> context,
        CancellationToken cancellationToken = default)
    {
        var state = context.State as Package1WorkflowState
            ?? throw new InvalidOperationException("Invalid workflow state");

        var exportRoot = !string.IsNullOrWhiteSpace(state.OutputPath)
            ? state.OutputPath
            : _libraryPathProvider.LibraryRoot;

        await ExecuteOperationAsync(
            "GalleryThumbnails",
            exportRoot,
            async () =>
            {
                var generated = await _galleryThumbnailService.GenerateAsync(exportRoot, cancellationToken);
                _logger.LogInformation("Gallery thumbnails generated for {ExportRoot}: {Generated} new", exportRoot, generated);
            },
            _logger);
    }
}

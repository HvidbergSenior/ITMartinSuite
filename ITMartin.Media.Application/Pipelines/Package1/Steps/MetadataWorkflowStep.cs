using ITMartin.Media.Application.Pipelines.Package1.Models;
using ITMartin.Media.Application.Pipelines.Package1.Orchestration;
using ITMartin.Media.Contracts.Contracts.Runtime.Helpers;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Pipelines.Package1.Steps;

public sealed class MetadataWorkflowStep
    : Package1WorkflowStepBase
{
    private readonly ILogger<MetadataWorkflowStep>
        _logger;

    private readonly IMediaDateService
        _mediaDateService;

    private readonly IImageMetadataService
        _imageMetadataService;

    private readonly IVideoMetadataService
        _videoMetadataService;

    private readonly IDocumentMetadataService
        _documentMetadataService;

    private readonly IGpsService
        _gpsService;

    public MetadataWorkflowStep(
        ILogger<MetadataWorkflowStep> logger,
        IMediaDateService mediaDateService,
        IImageMetadataService imageMetadataService,
        IVideoMetadataService videoMetadataService,
        IDocumentMetadataService documentMetadataService,
        IGpsService gpsService)
    {
        _logger =
            logger;

        _mediaDateService =
            mediaDateService;

        _imageMetadataService =
            imageMetadataService;

        _videoMetadataService =
            videoMetadataService;

        _documentMetadataService =
            documentMetadataService;

        _gpsService =
            gpsService;
    }

    public override string Name =>
        "Metadata";

    public override async Task ExecuteAsync<TState>(
        WorkflowExecutionContext<TState> context,
        CancellationToken cancellationToken = default)
    {
        var state =
            context.State as Package1WorkflowState
            ?? throw new InvalidOperationException(
                "Invalid workflow state");

        var total =
            state.MediaFiles.Count;

        var current = 0;

        foreach (var file in state.MediaFiles)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            current++;

            LogStepProgress(
                _logger,
                Name,
                current,
                total,
                file.FileName);

            await ExecuteOperationAsync(
                "ExtractMetadata",
                file.FileName,
                async () =>
                {
                    if (state.OverrideYear is null)
                    {
                        var result =
                            _mediaDateService.GetBestDate(
                                new MediaDateRequest(
                                    file.FullPath));

                        if (result.Date is not null)
                        {
                            file.SetDate(
                                result.Date,
                                result.IsReliable);
                        }
                    }

                    if (MediaTypeHelper.IsImage(file.FullPath))
                    {
                        var dimensions =
                            _imageMetadataService
                                .GetDimensions(
                                    file.FullPath);

                        var coordinates =
                            _gpsService
                                .GetCoordinates(
                                    file.FullPath);

                        if (coordinates is not null)
                        {
                            file.Latitude =
                                coordinates.Value.lat;

                            file.Longitude =
                                coordinates.Value.lng;
                        }

                        if (dimensions is not null)
                        {
                            file.Width =
                                dimensions.Value.Width;

                            file.Height =
                                dimensions.Value.Height;
                        }
                    }

                    await Task.CompletedTask;
                },
                _logger);
        }
    }
}
using ITMartin.Media.Application.Pipelines.Package1.Models;
using ITMartin.Media.Application.Pipelines.Package1.Orchestration;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Pipelines.Package1.Steps;

public sealed class MetadataWorkflowStep : IWorkflowStep
{
private readonly ILogger<MetadataWorkflowStep> _logger;
private readonly IMediaDateService  _mediaDateService;
private readonly IImageMetadataService  _imageMetadataService;
private readonly IVideoMetadataService  _videoMetadataService;
private readonly IDocumentMetadataService  _documentMetadataService;
private readonly IGpsService  _gpsService;


    public MetadataWorkflowStep(ILogger<MetadataWorkflowStep> logger, IMediaDateService mediaDateService, IImageMetadataService imageMetadataService, IVideoMetadataService videoMetadataService, IDocumentMetadataService documentMetadataService, IGpsService gpsService)
    {
        _logger = logger;
        _mediaDateService = mediaDateService;
        _imageMetadataService = imageMetadataService;
        _videoMetadataService = videoMetadataService;
        _documentMetadataService = documentMetadataService;
        _gpsService = gpsService;
    }

    public string Name => "Metadata";
    public async Task ExecuteAsync<TState>(
        WorkflowExecutionContext<TState> context,
        CancellationToken cancellationToken = default)
        where TState : class
    {
        _logger.LogInformation(
            "Executing {Step}",
            nameof(MetadataWorkflowStep));
        var state =
            context.State as Package1WorkflowState
            ?? throw new InvalidOperationException(
                "Invalid workflow state");

        cancellationToken.ThrowIfCancellationRequested();

        foreach (var file in state.MediaFiles)
        {
            _logger.LogInformation(
                "Extracting metadata for {File}",
                file.FullPath);

            var (date, reliable) =
                _mediaDateService.GetBestDate(
                    file.FullPath);

            if (date is not null)
            {
                file.SetDate(
                    date,
                    reliable);
            }

            if (file.IsImage)
            {
                var dimensions =
                    _imageMetadataService
                        .GetDimensions(
                            file.FullPath);

                var coordinates =
                    _gpsService.GetCoordinates(
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

            _logger.LogInformation(
                "Metadata {Count}/{Total}",
                state.MediaFiles.Count,
                state.MediaFiles.Count);
        }
    }
}
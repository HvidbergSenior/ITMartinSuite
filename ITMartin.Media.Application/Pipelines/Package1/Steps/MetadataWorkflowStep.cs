using ITMartin.Media.Application.Pipelines.Package1.Models;
using ITMartin.Media.Application.Pipelines.Package1.Orchestration;
using ITMartin.Media.Contracts.Contracts.Runtime.Enums;
using ITMartin.Media.Contracts.Contracts.Runtime.Helpers;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Persistence;
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

    private readonly IAudioMetadataService
        _audioMetadataService;

    private readonly IGpsService
        _gpsService;

    private readonly IWorkflowInstanceStore
        _workflowInstanceStore;

    public MetadataWorkflowStep(
        ILogger<MetadataWorkflowStep> logger,
        IMediaDateService mediaDateService,
        IImageMetadataService imageMetadataService,
        IVideoMetadataService videoMetadataService,
        IDocumentMetadataService documentMetadataService,
        IAudioMetadataService audioMetadataService,
        IGpsService gpsService,
        IWorkflowInstanceStore workflowInstanceStore)
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

        _audioMetadataService =
            audioMetadataService;

        _gpsService =
            gpsService;

        _workflowInstanceStore =
            workflowInstanceStore;
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

            if (current % 10 == 0 || current == total)
            {
                await _workflowInstanceStore.SetProgressAsync(
                    context.WorkflowId,
                    current,
                    total,
                    item: file.FileName,
                    cancellationToken: cancellationToken);
            }

            var ok = await ExecuteOperationAsync(
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
                                result.IsReliable,
                                result.IsYearOnly);
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

                    if (MediaTypeHelper.IsAudio(file.FullPath))
                    {
                        file.Artist =
                            _audioMetadataService.GetArtist(file.FullPath);

                        file.Title =
                            _audioMetadataService.GetTitle(file.FullPath);

                        file.TrackNumber =
                            _audioMetadataService.GetTrackNumber(file.FullPath);

                        file.Duration =
                            _audioMetadataService.GetDuration(file.FullPath);

                        // A ripped CD's tracks are usually already sitting in a
                        // folder named after the album even when the ID3 Album
                        // tag itself is blank - falling back to that keeps the
                        // Musik/{Artist}/{Album} export from scattering an
                        // otherwise-related set of tracks into "Ukendt album".
                        var album =
                            _audioMetadataService.GetAlbum(file.FullPath);

                        file.Album =
                            string.IsNullOrWhiteSpace(album)
                                ? Path.GetFileName(Path.GetDirectoryName(file.FullPath))
                                : album;
                    }

                    await Task.CompletedTask;
                },
                _logger);

            if (!ok)
                state.FailedFiles.Add(new FailedFile { FilePath = file.FullPath, Step = Name, Error = "Metadata extraction failed" });
        }
    }
}
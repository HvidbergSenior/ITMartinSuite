using ITMartin.Media.Application.Pipelines.QuickSort.Models;
using ITMartin.Media.Application.Pipelines.QuickSort.Orchestration;
using ITMartin.Media.Contracts.Contracts.Runtime.Enums;
using ITMartin.Media.Contracts.Contracts.Runtime.Helpers;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Persistence;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Pipelines.QuickSort.Steps;

public sealed class MetadataWorkflowStep
    : QuickSortWorkflowStepBase
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
            context.State as QuickSortWorkflowState
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
                        // Each extraction below is independent - a photo with no
                        // GPS still has usable dimensions, and one with unreadable
                        // EXIF still has a date. They used to share this method's
                        // single try/catch, so the FIRST one to throw silently
                        // discarded every later one for that file.
                        //
                        // That is not hypothetical: GetCameraModel was a
                        // NotImplementedException stub, so on the 2026-09-10 test
                        // run it threw for all 3,816 photos and took GPS and
                        // dimensions down with it every single time. The run still
                        // produced a plausible-looking library, because the date is
                        // read before this point - which is exactly why nobody
                        // noticed. Isolate them so one missing field can never
                        // again cost the others.
                        var dimensions =
                            TryExtract(
                                () => _imageMetadataService.GetDimensions(file.FullPath),
                                "dimensions", file.FullPath);

                        // Captured here, at step 8, rather than left unset:
                        // this service is already open on the file's EXIF for
                        // the dimensions above, so the camera name is free,
                        // and knowing it this early means every later step -
                        // and the Roter-manuelt.csv review list - can say
                        // WHICH camera a rotation problem came from instead of
                        // just "orientation unknown". The whole 2007-2010
                        // rotation problem on ToshibaTest turned out to be two
                        // specific Olympus bodies (see
                        // ImageConverterService.OrientationUnreliableModelSubstrings),
                        // which is only obvious once the model is recorded.
                        file.CameraModel =
                            TryExtract(
                                () => _imageMetadataService.GetCameraModel(file.FullPath),
                                "camera model", file.FullPath);

                        var coordinates =
                            TryExtract(
                                () => _gpsService.GetCoordinates(file.FullPath),
                                "GPS", file.FullPath);

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

                    if (MediaTypeHelper.IsVideo(file.FullPath))
                    {
                        // _videoMetadataService was injected here but never
                        // actually called - confirmed 2026-09-06, needed now
                        // so CleanupEvaluationWorkflowStep can tell a
                        // downloaded film/episode (typically 20+ min) from a
                        // personal clip (typically a few minutes) regardless
                        // of file size, which large personal videos on this
                        // library already share with commercial content.
                        // Already timeout-protected (30s) against a
                        // malformed/hanging file - see VideoMetadataService.
                        // ??= : MediaRulesWorkflowStep already fetched this
                        // for its own unplayable-video check and set it on
                        // the file - don't pay for a second ffprobe call.
                        file.Duration ??=
                            _videoMetadataService.GetDuration(file.FullPath);
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

    // One optional field, extracted without letting its absence cost the fields
    // around it. Returns null both when a file genuinely has no such metadata
    // (the common case - most photos have no GPS) and when reading it threw.
    //
    // The distinction is deliberately not made at the call site: neither case
    // is a reason to fail the file, and both leave the field unset. A throw is
    // logged at Warning so a systematic failure - one extractor breaking for
    // every file, as happened on 2026-09-10 - is visible in the log instead of
    // hiding behind a per-file "metadata extraction failed" that says nothing
    // about which of four extractions actually broke.
    private T? TryExtract<T>(Func<T?> extract, string what, string path)
    {
        try
        {
            return extract();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Could not read {What} from {Path} - continuing with the other metadata",
                what,
                path);

            return default;
        }
    }
}
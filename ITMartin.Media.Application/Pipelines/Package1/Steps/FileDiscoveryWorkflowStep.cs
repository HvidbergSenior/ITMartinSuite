using ITMartin.Media.Application.Interfaces;
using ITMartin.Media.Application.Pipelines.Package1.Models;
using ITMartin.Media.Application.Pipelines.Package1.Orchestration;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Persistence;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Pipelines.Package1.Steps;

public sealed class FileDiscoveryWorkflowStep
    : Package1WorkflowStepBase
{
    private readonly ILogger<FileDiscoveryWorkflowStep>
        _logger;

    private readonly IFileScanner
        _fileScanner;

    private readonly IMediaTypeResolver
        _mediaTypeResolver;

    private readonly IMediaDateService
        _mediaDateService;

    private readonly IWorkflowInstanceStore
        _workflowInstanceStore;

    public FileDiscoveryWorkflowStep(
        IFileScanner fileScanner,
        IMediaTypeResolver mediaTypeResolver,
        IMediaDateService mediaDateService,
        IWorkflowInstanceStore workflowInstanceStore,
        ILogger<FileDiscoveryWorkflowStep> logger)
    {
        _fileScanner =
            fileScanner;

        _mediaTypeResolver =
            mediaTypeResolver;

        _mediaDateService =
            mediaDateService;

        _workflowInstanceStore =
            workflowInstanceStore;

        _logger =
            logger;
    }

    public override string Name =>
        "FileDiscovery";

    public override async Task ExecuteAsync<TState>(
        WorkflowExecutionContext<TState> context,
        CancellationToken cancellationToken = default)
    {
        var state =
            context.State as Package1WorkflowState
            ?? throw new InvalidOperationException(
                "Invalid workflow state");

        if (state.MediaFiles.Count > 0)
        {
            return;
        }

        await ExecuteOperationAsync(
            "ScanFiles",
            state.RootPath,
            async () =>
            {
                var files =
                    (await _fileScanner.ScanAsync(
                        state.RootPath,
                        cancellationToken)).ToList();

                var total = files.Count;
                var current = 0;
                var result = new List<MediaFile>(total);
                var categoryCounts = new Dictionary<string, int>();

                foreach (var path in files)
                {
                    current++;

                    // One malformed file (bad date-in-filename, unreadable, etc.) must
                    // not abort discovery for the other thousands - skip it and log,
                    // rather than losing the whole scan to a single bad file.
                    try
                    {
                        var mediaType = _mediaTypeResolver.Resolve(path);
                        var typeName = mediaType.ToString();
                        categoryCounts[typeName] =
                            categoryCounts.GetValueOrDefault(typeName) + 1;

                        LogStepProgress(
                            _logger,
                            Name,
                            current,
                            total,
                            Path.GetFileName(path));

                        if (current % 10 == 0 || current == total)
                        {
                            await _workflowInstanceStore.SetProgressAsync(
                                context.WorkflowId,
                                current,
                                total,
                                item: Path.GetFileName(path),
                                counts: categoryCounts,
                                cancellationToken: cancellationToken);
                        }

                        var dateResult =
                            _mediaDateService.GetBestDate(
                                new MediaDateRequest(
                                    path,
                                    state.OverrideYear));

                        result.Add(new MediaFile(
                            path,
                            dateResult.Date,
                            mediaType,
                            new FileInfo(path).Length,
                            dateResult.IsReliable));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Skipping file during discovery: {Path}", path);
                    }
                }

                state.MediaFiles = result;
            },
            _logger);
    }
}

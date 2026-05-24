using ITMartin.Media.Application.Interfaces;
using ITMartin.Media.Application.Pipelines.Package1.Models;
using ITMartin.Media.Application.Pipelines.Package1.Orchestration;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Pipelines.Package1.Steps;

public sealed class FileDiscoveryWorkflowStep
    : IWorkflowStep
{
    private readonly ILogger<FileDiscoveryWorkflowStep> _logger;

    private readonly IFileScanner _fileScanner;

    private readonly IMediaTypeResolver _mediaTypeResolver;

    private readonly IMediaDateService _mediaDateService;

    public FileDiscoveryWorkflowStep(
        IFileScanner fileScanner,
        IMediaTypeResolver mediaTypeResolver,
        IMediaDateService mediaDateService,
        ILogger<FileDiscoveryWorkflowStep> logger)
    {
        _fileScanner = fileScanner;
        _mediaTypeResolver = mediaTypeResolver;
        _mediaDateService = mediaDateService;
        _logger = logger;
    }

    public string Name => "FileDiscovery";

    public async Task ExecuteAsync<TState>(
        WorkflowExecutionContext<TState> context,
        CancellationToken cancellationToken = default)
        where TState : class
    {
        _logger.LogInformation(
            "Executing {Step}",
            nameof(FileDiscoveryWorkflowStep));

        var state =
            context.State as Package1WorkflowState
            ?? throw new InvalidOperationException(
                "Invalid workflow state");
        _logger.LogWarning(
            "OVERRIDE YEAR IN DISCOVERY: {Year}",
            state.OverrideYear);
        if (state.MediaFiles.Count > 0)
        {
            return;
        }

        var files =
            await _fileScanner.ScanAsync(
                state.RootPath,
                cancellationToken);

        state.MediaFiles =
            files
                .Select(path =>
                {
                    var dateResult =
                        _mediaDateService.GetBestDate(
                            new MediaDateRequest(
                                path,
                                state.OverrideYear));

                    _logger.LogInformation(
                        "[DATE] {Path} -> {Date} ({Source}) Reliable={Reliable}",
                        path,
                        dateResult.Date,
                        dateResult.Source,
                        dateResult.IsReliable);

                    return new MediaFile(
                        path,
                        dateResult.Date,
                        _mediaTypeResolver.Resolve(path),
                        new FileInfo(path).Length,
                        dateResult.IsReliable);
                })
                .ToList();
    }
}
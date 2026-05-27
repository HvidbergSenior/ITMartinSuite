using ITMartin.Media.Application.Interfaces;
using ITMartin.Media.Application.Pipelines.Package1.Models;
using ITMartin.Media.Application.Pipelines.Package1.Orchestration;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
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

    public FileDiscoveryWorkflowStep(
        IFileScanner fileScanner,
        IMediaTypeResolver mediaTypeResolver,
        IMediaDateService mediaDateService,
        ILogger<FileDiscoveryWorkflowStep> logger)
    {
        _fileScanner =
            fileScanner;

        _mediaTypeResolver =
            mediaTypeResolver;

        _mediaDateService =
            mediaDateService;

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
                    await _fileScanner.ScanAsync(
                        state.RootPath,
                        cancellationToken);

                var total =
                    files.Count();

                var current = 0;

                state.MediaFiles =
                    files
                        .Select(path =>
                        {
                            current++;

                            LogStepProgress(
                                _logger,
                                Name,
                                current,
                                total,
                                Path.GetFileName(path));

                            var dateResult =
                                _mediaDateService.GetBestDate(
                                    new MediaDateRequest(
                                        path,
                                        state.OverrideYear));

                            return new MediaFile(
                                path,
                                dateResult.Date,
                                _mediaTypeResolver.Resolve(path),
                                new FileInfo(path).Length,
                                dateResult.IsReliable);
                        })
                        .ToList();
            },
            _logger);
    }
}
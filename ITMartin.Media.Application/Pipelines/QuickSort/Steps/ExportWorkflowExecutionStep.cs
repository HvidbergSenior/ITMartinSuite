using ITMartin.Media.Application.Interfaces;
using ITMartin.Media.Application.Pipelines.QuickSort.Models;
using ITMartin.Media.Application.Pipelines.QuickSort.Orchestration;
using ITMartin.Media.Application.Pipelines.QuickSort.Services;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Pipelines.QuickSort.Steps;

public sealed class ExportWorkflowExecutionStep
    : QuickSortWorkflowStepBase
{
    private readonly QuickSortExportService
        _exportService;

    private readonly ILibraryPathProvider
        _libraryPathProvider;

    private readonly ILogger<
            ExportWorkflowExecutionStep>
        _logger;

    private readonly QuickSortManifestWriter
        _manifestWriter;

    public ExportWorkflowExecutionStep(
        QuickSortExportService exportService,
        ILibraryPathProvider libraryPathProvider,
        ILogger<ExportWorkflowExecutionStep> logger,
        QuickSortManifestWriter manifestWriter)
    {
        _exportService =
            exportService;

        _libraryPathProvider =
            libraryPathProvider;

        _logger =
            logger;

        _manifestWriter =
            manifestWriter;
    }

    public override string Name =>
        "Export";

    public override async Task ExecuteAsync<TState>(
        WorkflowExecutionContext<TState> context,
        CancellationToken cancellationToken = default)
    {
        var state =
            context.State as QuickSortWorkflowState
            ?? throw new InvalidOperationException(
                "Invalid workflow state");

        var exportRoot =
            !string.IsNullOrWhiteSpace(state.OutputPath)
                ? state.OutputPath
                : _libraryPathProvider.LibraryRoot;

        Directory.CreateDirectory(
            exportRoot);

        await ExecuteOperationAsync(
            "ExportLibrary",
            exportRoot,
            async () =>
            {
                var result =
                    await _exportService.ExportAsync(
                        state.MediaFiles,
                        exportRoot,
                        async (
                            current,
                            total,
                            file,
                            _) =>
                        {
                            LogStepProgress(
                                _logger,
                                Name,
                                current,
                                total,
                                file);

                            await Task.CompletedTask;
                        });

                state.ExportResult =
                    result;

                var manifest =
                    new QuickSortManifest
                    {
                        WorkflowId =
                            context.WorkflowId,

                        RootPath =
                            exportRoot,

                        MediaFiles =
                            state.MediaFiles.ToList(),

                        FileCount =
                            state.MediaFiles.Count,

                        CreatedAtUtc =
                            DateTimeOffset.UtcNow
                    };

                await _manifestWriter.WriteAsync(
                    exportRoot,
                    manifest,
                    cancellationToken);

                if (state.FailedFiles.Count > 0)
                {
                    var failedFilesPath = Path.Combine(exportRoot, "_failed_files.txt");
                    var lines = state.FailedFiles
                        .Select(f => $"[{f.Step}] {f.FilePath} — {f.Error}");
                    await File.WriteAllLinesAsync(failedFilesPath, lines, cancellationToken);
                    _logger.LogWarning("{Count} files failed processing — see {Path}", state.FailedFiles.Count, failedFilesPath);
                }
            },
            _logger);
    }
}
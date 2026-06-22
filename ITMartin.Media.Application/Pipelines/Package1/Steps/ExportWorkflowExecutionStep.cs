using ITMartin.Media.Application.Interfaces;
using ITMartin.Media.Application.Pipelines.Package1.Models;
using ITMartin.Media.Application.Pipelines.Package1.Orchestration;
using ITMartin.Media.Application.Pipelines.Package1.Services;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Pipelines.Package1.Steps;

public sealed class ExportWorkflowExecutionStep
    : Package1WorkflowStepBase
{
    private readonly Package1ExportService
        _exportService;

    private readonly ILibraryPathProvider
        _libraryPathProvider;

    private readonly ILogger<
            ExportWorkflowExecutionStep>
        _logger;

    private readonly Package1ManifestWriter
        _manifestWriter;

    public ExportWorkflowExecutionStep(
        Package1ExportService exportService,
        ILibraryPathProvider libraryPathProvider,
        ILogger<ExportWorkflowExecutionStep> logger,
        Package1ManifestWriter manifestWriter)
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
            context.State as Package1WorkflowState
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
                    new Package1Manifest
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
            },
            _logger);
    }
}
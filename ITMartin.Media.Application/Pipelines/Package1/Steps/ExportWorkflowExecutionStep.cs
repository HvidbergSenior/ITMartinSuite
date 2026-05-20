// File: ExportWorkflowExecutionStep.cs

using ITMartin.Media.Application.Interfaces;
using ITMartin.Media.Application.Pipelines.Package1.Orchestration;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Pipelines.Package1.Steps;

public sealed class ExportWorkflowExecutionStep
    : IWorkflowStep
{
    private readonly Package1ExportService
        _exportService;

    private readonly ILibraryPathProvider
        _libraryPathProvider;

    private readonly ILogger<
            ExportWorkflowExecutionStep>
        _logger;

    public ExportWorkflowExecutionStep(
        Package1ExportService exportService,
        ILibraryPathProvider libraryPathProvider,
        ILogger<ExportWorkflowExecutionStep> logger)
    {
        _exportService = exportService;
        _libraryPathProvider = libraryPathProvider;
        _logger = logger;
    }

    public string Name => "Export";

    public async Task ExecuteAsync<TState>(
        WorkflowExecutionContext<TState> context,
        CancellationToken cancellationToken = default)
        where TState : class
    {
        _logger.LogInformation(
            "Executing {Step}",
            nameof(ExportWorkflowExecutionStep));

        var state =
            context.State as Package1WorkflowState
            ?? throw new InvalidOperationException(
                "Invalid workflow state");
        _logger.LogInformation(
            "MediaFiles count: {Count}",
            state.MediaFiles.Count);
        var now =
            DateTime.UtcNow;

        var exportRoot =
            Path.Combine(
                _libraryPathProvider.LibraryRoot,
                now.Year.ToString(),
                now.Month.ToString("00"),
                now.Day.ToString("00"));

        Directory.CreateDirectory(
            exportRoot);

        _logger.LogInformation(
            "Export root: {ExportRoot}",
            exportRoot);

        _logger.LogInformation(
            "Exporting {Count} files",
            state.MediaFiles.Count);

        var result =
            await _exportService.ExportAsync(
                state.MediaFiles,
                exportRoot);

        state.ExportResult = result;

        _logger.LogInformation(
            "Export completed");
    }
}
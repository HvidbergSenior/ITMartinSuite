using ITMartin.Media.Application.Pipelines.Package1.Orchestration;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Pipelines.Package1.Steps;

public sealed class ExportWorkflowExecutionStep
    : IWorkflowStep
{
    private readonly Package1ExportService
        _exportService;

    private readonly ILogger<
            ExportWorkflowExecutionStep>
        _logger;

    public ExportWorkflowExecutionStep(
        Package1ExportService exportService,
        ILogger<ExportWorkflowExecutionStep> logger)
    {
        _exportService = exportService;
        _logger = logger;
    }

    public string Name => "Export";

    public async Task ExecuteAsync<TState>(
        WorkflowExecutionContext<TState> context,
        CancellationToken cancellationToken = default)
        where TState : class
    {
        var state =
            context.State as Package1WorkflowState
            ?? throw new InvalidOperationException(
                "Invalid workflow state");

        _logger.LogInformation(
            "Starting export");

        var exportRoot =
            Path.Combine(
                state.RootPath,
                "_exports");

        var result =
            await _exportService.ExportAsync(
                state.MediaFiles,
                exportRoot);

        state.ExportResult = result;

        _logger.LogInformation(
            "Export completed");
    }
}
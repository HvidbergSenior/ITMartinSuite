using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Pipelines.Package2.Steps;

public sealed class QualityEvaluationWorkflowStep
    : Package2WorkflowStepBase
{
    private readonly ILogger<
            QualityEvaluationWorkflowStep>
        _logger;

    public override string Name =>
        nameof(QualityEvaluationWorkflowStep);

    public QualityEvaluationWorkflowStep(
        ILogger<QualityEvaluationWorkflowStep> logger)
    {
        _logger =
            logger;
    }

    public override async Task ExecuteAsync<TState>(
        WorkflowExecutionContext<TState> context,
        CancellationToken cancellationToken = default)
    {
        if (context.State is not Package2WorkflowState state)
        {
            return;
        }

        var items =
            state.Items
                .Where(x =>
                    !x.Failed &&
                    x.CurrentWorkingPath is not null &&
                    !AlreadyExecuted(x, Name))
                .ToList();

        var total =
            items.Count;

        var current = 0;

        foreach (var item in items)
        {
            current++;

            _logger.LogInformation(
                "[{Step}] {Current}/{Total} {File}",
                Name,
                current,
                total,
                item.CurrentWorkingPath);

            await ExecuteOperationAsync(
                item,
                Name,
                async () =>
                {
                    var fileInfo =
                        new FileInfo(
                            item.CurrentWorkingPath!);

                    if (!fileInfo.Exists)
                    {
                        throw new InvalidOperationException(
                            "Enhanced file missing.");
                    }

                    await Task.CompletedTask;
                },
                _logger);
        }
    }
}
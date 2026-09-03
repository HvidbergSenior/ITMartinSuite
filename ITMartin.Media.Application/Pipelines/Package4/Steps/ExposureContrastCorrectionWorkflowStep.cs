using ITMartin.Media.Application.Pipelines.AnalogDigitize.Steps;
using ITMartin.Media.Contracts.Contracts.Runtime.Enums;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Pipelines.Package4.Steps;

public sealed class ExposureContrastCorrectionWorkflowStep : AnalogDigitizeWorkflowStepBase
{
    private readonly ILogger<ExposureContrastCorrectionWorkflowStep> _logger;
    public override string Name => nameof(ExposureContrastCorrectionWorkflowStep);

    public ExposureContrastCorrectionWorkflowStep(ILogger<ExposureContrastCorrectionWorkflowStep> logger)
    {
        _logger = logger;
    }

    public override async Task ExecuteAsync<TState>(WorkflowExecutionContext<TState> context, CancellationToken cancellationToken = default)
    {
        if (context.State is not Package4WorkflowState state) return;
        if (!state.EnableExposureContrast)
        {
            _logger.LogInformation("Skipping exposure/contrast correction");
            return;
        }

        foreach (var item in state.Items.Where(x => !x.Failed && x.MediaKind == MediaKind.Video && !AlreadyExecuted(x, Name)))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await ExecuteOperationAsync(item, Name, () =>
            {
                item.VideoFilters.Add("eq=contrast=1.15:brightness=0.02:gamma=1.05");
                return Task.CompletedTask;
            }, _logger);
        }
    }
}

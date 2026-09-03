using ITMartin.Media.Application.Pipelines.AnalogDigitize.Steps;
using ITMartin.Media.Contracts.Contracts.Runtime.Enums;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Pipelines.Package4.Steps;

// Trims the shaky black edges vidstabtransform's "crop=black" leaves behind
// by cropping in slightly and rescaling to the original frame size. Only
// meaningful when stabilization actually ran - skipped entirely otherwise.
public sealed class StabilizedCropWorkflowStep : AnalogDigitizeWorkflowStepBase
{
    private readonly ILogger<StabilizedCropWorkflowStep> _logger;
    public override string Name => nameof(StabilizedCropWorkflowStep);

    public StabilizedCropWorkflowStep(ILogger<StabilizedCropWorkflowStep> logger)
    {
        _logger = logger;
    }

    public override async Task ExecuteAsync<TState>(WorkflowExecutionContext<TState> context, CancellationToken cancellationToken = default)
    {
        if (context.State is not Package4WorkflowState state) return;
        if (!state.EnableStabilizedCrop || !state.EnableStabilization)
        {
            _logger.LogInformation("Skipping stabilized reframe/crop");
            return;
        }

        foreach (var item in state.Items.Where(x => !x.Failed && x.MediaKind == MediaKind.Video && !AlreadyExecuted(x, Name)))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await ExecuteOperationAsync(item, Name, () =>
            {
                item.VideoFilters.Add("crop=iw*0.92:ih*0.92,scale=iw/0.92:ih/0.92");
                return Task.CompletedTask;
            }, _logger);
        }
    }
}

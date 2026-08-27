using ITMartin.Media.Application.Pipelines.Package2.Steps;
using ITMartin.Media.Contracts.Contracts.Runtime.Enums;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Pipelines.Package4.Steps;

// ffmpeg has no dedicated wind-noise filter - wind energy concentrates below
// ~150Hz, well above the rumble/hum range a plain highpass already covers,
// so this is a second, more aggressive highpass specifically for outdoor
// handheld clips rather than a true spectral wind-noise model.
public sealed class WindNoiseReductionWorkflowStep : Package2WorkflowStepBase
{
    private readonly ILogger<WindNoiseReductionWorkflowStep> _logger;
    public override string Name => nameof(WindNoiseReductionWorkflowStep);

    public WindNoiseReductionWorkflowStep(ILogger<WindNoiseReductionWorkflowStep> logger)
    {
        _logger = logger;
    }

    public override async Task ExecuteAsync<TState>(WorkflowExecutionContext<TState> context, CancellationToken cancellationToken = default)
    {
        if (context.State is not Package4WorkflowState state) return;
        if (!state.EnableWindNoiseReduction)
        {
            _logger.LogInformation("Skipping wind noise reduction");
            return;
        }

        foreach (var item in state.Items.Where(x => !x.Failed && x.MediaKind == MediaKind.Video && !AlreadyExecuted(x, Name)))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await ExecuteOperationAsync(item, Name, () =>
            {
                item.AudioFilters.Add("highpass=f=150");
                return Task.CompletedTask;
            }, _logger);
        }
    }
}

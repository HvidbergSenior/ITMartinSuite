using ITMartin.Media.Application.Pipelines.AnalogDigitize.Steps;
using ITMartin.Media.Contracts.Contracts.Runtime.Enums;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Pipelines.Package4.Steps;

// Corrects color casts (e.g. the yellow-orange tint typical of indoor
// incandescent/warm-LED lighting on phone footage) via a mild cool-shadow /
// warm-highlight balance rather than a full auto-white-balance analysis -
// ffmpeg has no true AWB filter, so this is a fixed nudge, not per-clip
// adaptive correction.
public sealed class WhiteBalanceCorrectionWorkflowStep : AnalogDigitizeWorkflowStepBase
{
    private readonly ILogger<WhiteBalanceCorrectionWorkflowStep> _logger;
    public override string Name => nameof(WhiteBalanceCorrectionWorkflowStep);

    public WhiteBalanceCorrectionWorkflowStep(ILogger<WhiteBalanceCorrectionWorkflowStep> logger)
    {
        _logger = logger;
    }

    public override async Task ExecuteAsync<TState>(WorkflowExecutionContext<TState> context, CancellationToken cancellationToken = default)
    {
        if (context.State is not Package4WorkflowState state) return;
        if (!state.EnableWhiteBalance)
        {
            _logger.LogInformation("Skipping white balance correction");
            return;
        }

        foreach (var item in state.Items.Where(x => !x.Failed && x.MediaKind == MediaKind.Video && !AlreadyExecuted(x, Name)))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await ExecuteOperationAsync(item, Name, () =>
            {
                item.VideoFilters.Add("colorbalance=rs=0.03:gs=0.0:bs=-0.03:rm=0.02:bm=-0.02");
                return Task.CompletedTask;
            }, _logger);
        }
    }
}

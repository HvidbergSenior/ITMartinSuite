using ITMartin.Media.Application.Pipelines.AnalogDigitize.Steps;
using ITMartin.Media.Contracts.Contracts.Runtime.Enums;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Pipelines.Package4.Steps;

// Rumble cut (handling noise, low-frequency wind rumble) + a presence boost
// around 3kHz where speech intelligibility lives.
public sealed class AudioEqWorkflowStep : AnalogDigitizeWorkflowStepBase
{
    private readonly ILogger<AudioEqWorkflowStep> _logger;
    public override string Name => nameof(AudioEqWorkflowStep);

    public AudioEqWorkflowStep(ILogger<AudioEqWorkflowStep> logger)
    {
        _logger = logger;
    }

    public override async Task ExecuteAsync<TState>(WorkflowExecutionContext<TState> context, CancellationToken cancellationToken = default)
    {
        if (context.State is not Package4WorkflowState state) return;
        if (!state.EnableAudioEq)
        {
            _logger.LogInformation("Skipping audio EQ");
            return;
        }

        foreach (var item in state.Items.Where(x => !x.Failed && x.MediaKind == MediaKind.Video && !AlreadyExecuted(x, Name)))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await ExecuteOperationAsync(item, Name, () =>
            {
                item.AudioFilters.Add("highpass=f=90,equalizer=f=3000:width_type=o:width=1.5:g=3");
                return Task.CompletedTask;
            }, _logger);
        }
    }
}

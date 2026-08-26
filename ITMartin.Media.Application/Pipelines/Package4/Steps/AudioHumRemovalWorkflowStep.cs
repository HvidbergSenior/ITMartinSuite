using ITMartin.Media.Application.Pipelines.Package2.Steps;
using ITMartin.Media.Contracts.Contracts.Runtime.Enums;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Pipelines.Package4.Steps;

// 50Hz mains hum (EU) - rarely triggers on phone-mic audio, but cheap to
// include unconditionally rather than trying to detect the source's region.
public sealed class AudioHumRemovalWorkflowStep : Package2WorkflowStepBase
{
    private readonly ILogger<AudioHumRemovalWorkflowStep> _logger;
    public override string Name => nameof(AudioHumRemovalWorkflowStep);

    public AudioHumRemovalWorkflowStep(ILogger<AudioHumRemovalWorkflowStep> logger)
    {
        _logger = logger;
    }

    public override async Task ExecuteAsync<TState>(WorkflowExecutionContext<TState> context, CancellationToken cancellationToken = default)
    {
        if (context.State is not Package4WorkflowState state) return;
        if (!state.EnableHumRemoval)
        {
            _logger.LogInformation("Skipping hum removal");
            return;
        }

        foreach (var item in state.Items.Where(x => !x.Failed && x.MediaKind == MediaKind.Video && !AlreadyExecuted(x, Name)))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await ExecuteOperationAsync(item, Name, () =>
            {
                item.AudioFilters.Add("bandreject=f=50:width_type=h:w=4");
                return Task.CompletedTask;
            }, _logger);
        }
    }
}

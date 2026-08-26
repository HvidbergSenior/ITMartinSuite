using ITMartin.Media.Application.Pipelines.Package2.Steps;
using ITMartin.Media.Contracts.Contracts.Runtime.Enums;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Pipelines.Package4.Steps;

public sealed class AudioCompressionWorkflowStep : Package2WorkflowStepBase
{
    private readonly ILogger<AudioCompressionWorkflowStep> _logger;
    public override string Name => nameof(AudioCompressionWorkflowStep);

    public AudioCompressionWorkflowStep(ILogger<AudioCompressionWorkflowStep> logger)
    {
        _logger = logger;
    }

    public override async Task ExecuteAsync<TState>(WorkflowExecutionContext<TState> context, CancellationToken cancellationToken = default)
    {
        if (context.State is not Package4WorkflowState state) return;
        if (!state.EnableAudioCompression)
        {
            _logger.LogInformation("Skipping audio compression");
            return;
        }

        foreach (var item in state.Items.Where(x => !x.Failed && x.MediaKind == MediaKind.Video && !AlreadyExecuted(x, Name)))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await ExecuteOperationAsync(item, Name, () =>
            {
                item.AudioFilters.Add("acompressor=threshold=-18dB:ratio=3:attack=5:release=50");
                return Task.CompletedTask;
            }, _logger);
        }
    }
}

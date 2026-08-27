using ITMartin.Media.Application.Pipelines.Package2.Steps;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Pipelines.Package4.Steps;

public sealed class SocialClipPreparationWorkflowStep
    : Package2WorkflowStepBase
{
    private readonly ILogger<SocialClipPreparationWorkflowStep> _logger;

    public override string Name => nameof(SocialClipPreparationWorkflowStep);

    public SocialClipPreparationWorkflowStep(ILogger<SocialClipPreparationWorkflowStep> logger)
    {
        _logger = logger;
    }

    public override async Task ExecuteAsync<TState>(
        WorkflowExecutionContext<TState> context,
        CancellationToken cancellationToken = default)
    {
        if (context.State is not Package4WorkflowState state)
        {
            throw new InvalidOperationException("Invalid workflow state.");
        }

        var workingDirectory = Path.Combine(state.WorkingDirectory, "working");
        var checkpointDirectory = Path.Combine(state.WorkingDirectory, "checkpoints");

        Directory.CreateDirectory(state.WorkingDirectory);
        Directory.CreateDirectory(workingDirectory);
        Directory.CreateDirectory(checkpointDirectory);

        var total = state.Items.Count;
        var current = 0;

        foreach (var item in state.Items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            current++;

            _logger.LogInformation("[{Step}] {Current}/{Total} {File}", Name, current, total, item.NormalizedPath);

            await ExecuteOperationAsync(item, Name, async () =>
            {
                if (!File.Exists(item.NormalizedPath))
                {
                    throw new InvalidOperationException("Normalized file does not exist.");
                }

                var fileName = Path.GetFileName(item.NormalizedPath);
                var workingPath = Path.Combine(workingDirectory, fileName);

                File.Copy(item.NormalizedPath, workingPath, overwrite: true);

                item.CurrentWorkingPath = workingPath;

                await Task.CompletedTask;
            }, _logger);
        }
    }
}

using ITMartin.Media.Contracts.Contracts.Runtime.Enums;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;

namespace ITMartin.Media.Application.Pipelines.Package2.Steps;

public sealed class VideoDenoiseWorkflowStep
    : IWorkflowStep
{
    public string Name =>
        nameof(VideoDenoiseWorkflowStep);

    public Task ExecuteAsync<TState>(
        WorkflowExecutionContext<TState> context,
        CancellationToken cancellationToken = default)
        where TState : class
    {
        if (context.State is not Package2WorkflowState state)
        {
            return Task.CompletedTask;
        }

        if (!state.EnableDenoise)
        {
            return Task.CompletedTask;
        }

        string filter =
            state.RestorationProfile switch
            {
                RestorationProfile.VHSAggressive
                    => "hqdn3d=3:3:2:2",

                RestorationProfile.FamilyArchive
                    => "hqdn3d=1.5:1.5:1:1",

                _ => "hqdn3d=2:2:1.5:1.5"
            };

        state.VideoPipeline.Add(
            filter);

        return Task.CompletedTask;
    }
}
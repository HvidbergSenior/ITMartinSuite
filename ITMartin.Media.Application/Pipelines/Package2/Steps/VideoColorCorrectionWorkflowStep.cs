using ITMartin.Media.Contracts.Contracts.Runtime.Enums;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;

namespace ITMartin.Media.Application.Pipelines.Package2.Steps;

public sealed class VideoColorCorrectionWorkflowStep
    : IWorkflowStep
{
    public string Name =>
        nameof(VideoColorCorrectionWorkflowStep);

    public Task ExecuteAsync<TState>(
        WorkflowExecutionContext<TState> context,
        CancellationToken cancellationToken = default)
        where TState : class
    {
        if (context.State is not Package2WorkflowState state)
        {
            return Task.CompletedTask;
        }

        if (!state.EnableColorCorrection)
        {
            return Task.CompletedTask;
        }

        string filter =
            state.RestorationProfile switch
            {
                RestorationProfile.VHSAggressive
                    => "eq=contrast=1.1:saturation=1.15:brightness=0.01",

                RestorationProfile.FamilyArchive
                    => "eq=contrast=1.05:saturation=1.08",

                _ => "eq=contrast=1.08:saturation=1.1"
            };

        state.VideoPipeline.Add(
            filter);

        return Task.CompletedTask;
    }
}
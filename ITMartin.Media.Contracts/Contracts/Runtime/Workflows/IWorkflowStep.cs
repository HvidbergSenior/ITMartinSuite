using ITMartin.Media.Contracts.Configuration;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;

namespace ITMartin.Media.Contracts.Contracts.Runtime.Workflows;

public interface IWorkflowStep
{
    string Name { get; }

    Task ExecuteAsync<TState>(
        WorkflowExecutionContext<TState> context,
        CancellationToken cancellationToken = default)
        where TState : class;
    public interface IPipelineStep
    {
        bool IsEnabled(
            Hi8PipelineOptions options);

        string BuildFilter(
            Hi8PipelineOptions options);
    }
}
// File: ITMartin.Media.Infrastructure/Workflows/InMemoryWorkflowRuntime.cs

using ITMartin.Media.Application.Abstractions.Queues;
using ITMartin.Media.Infrastructure.Contracts.Messages;

namespace ITMartin.Media.Application.Abstractions.Workflows;

public sealed class InMemoryWorkflowRuntime(
    IQueueProducer<WorkflowExecutionMessage> queueProducer)
    : IWorkflowRuntime
{
    public async Task StartAsync(
        WorkflowExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        var message = new WorkflowExecutionMessage(
            request.WorkflowId,
            request.WorkflowName,
            request.CorrelationId,
            request.Input);

        await queueProducer.EnqueueAsync(
            message,
            cancellationToken);
    }
}
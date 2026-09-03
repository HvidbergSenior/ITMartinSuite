using ITMartin.Media.Application.Abstractions.Events;
using ITMartin.Media.Application.Abstractions.Orchestration;
using ITMartin.Media.Application.Abstractions.Scanning;
using ITMartin.Media.Application.Events.Scanning;
using ITMartin.Media.Application.Pipelines.QuickSort.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Persistence;
using Microsoft.Extensions.Logging;

namespace ITMartin.Media.Application.Pipelines.QuickSort.Orchestration;

public sealed class QuickSortWorkflowOrchestrator
    : IScanOrchestrator
{
    private readonly IWorkflowInstanceStore
        _workflowInstanceStore;

    private readonly QuickSortWorkflowDefinition
        _workflowDefinition;

    private readonly IScanSessionRepository
        _repository;

    private readonly IEventPublisher
        _eventPublisher;

    private readonly IWorkflowCheckpointStore
        _workflowCheckpointStore;

    private readonly ILogger<
            QuickSortWorkflowOrchestrator>
        _logger;

    public QuickSortWorkflowOrchestrator(
        IWorkflowInstanceStore workflowInstanceStore,
        QuickSortWorkflowDefinition workflowDefinition,
        IScanSessionRepository repository,
        IEventPublisher eventPublisher,
        IWorkflowCheckpointStore workflowCheckpointStore,
        ILogger<QuickSortWorkflowOrchestrator> logger)
    {
        _workflowInstanceStore =
            workflowInstanceStore;

        _workflowDefinition =
            workflowDefinition;

        _repository =
            repository;

        _eventPublisher =
            eventPublisher;

        _workflowCheckpointStore =
            workflowCheckpointStore;

        _logger =
            logger;
    }

    public async Task<Guid> StartAsync(
        QuickSortWorkflowState request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RootPath))
        {
            throw new InvalidOperationException(
                "RootPath is required.");
        }
        var session =
            new ScanSession
            {
                Id = Guid.NewGuid(),
                RootPath = request.RootPath,
                Status = "Running",
                StartedAtUtc = DateTimeOffset.UtcNow
            };

        await _repository.CreateAsync(
            session,
            cancellationToken);

        await _eventPublisher.PublishAsync(
            new ScanStartedEvent(
                Guid.NewGuid(),
                session.Id,
                request.RootPath,
                DateTimeOffset.UtcNow),
            cancellationToken);

        await _workflowInstanceStore.CreateAsync(
            session.Id,
            _workflowDefinition.Name,
            cancellationToken);

        await _workflowCheckpointStore.SaveCheckpointAsync(
            session.Id,
            _workflowDefinition.Name,
            "Initial",
            request,
            cancellationToken);
        
        await _workflowInstanceStore.SetRunningStepAsync(
            session.Id,
            "FileDiscoveryWorkflowStep",
            cancellationToken);

        return session.Id;
    }

    public async Task ResumeAsync(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "ResumeAsync started for {SessionId}",
            sessionId);

        var session =
            await _repository.GetAsync(
                sessionId,
                cancellationToken);

        if (session is null)
        {
            _logger.LogWarning(
                "Session not found");

            return;
        }

        _logger.LogInformation(
            "Session found");

        session.Status = "Running";

        await _repository.UpdateAsync(
            session,
            cancellationToken);

        var state =
            await _workflowCheckpointStore
                .LoadLatestCheckpointAsync<
                    QuickSortWorkflowState>(
                    sessionId,
                    cancellationToken);
        
        if (state is null)
        {
            _logger.LogWarning(
                "Checkpoint not found");

            return;
        }

        _logger.LogInformation(
            "Checkpoint loaded");

        foreach (var step in _workflowDefinition.Steps)
        {
            _logger.LogInformation(
                "Executing step {StepName}",
                step.Name);

            await _workflowInstanceStore
                .SetRunningStepAsync(
                    sessionId,
                    step.Name,
                    cancellationToken);

            var context =
                new WorkflowExecutionContext<
                    QuickSortWorkflowState>
                {
                    WorkflowId = sessionId,
                    WorkflowName = _workflowDefinition.Name,
                    State = state
                };

            await step.ExecuteAsync(
                context,
                cancellationToken);

            await _workflowCheckpointStore
                .SaveCheckpointAsync(
                    sessionId,
                    _workflowDefinition.Name,
                    step.Name,
                    state,
                    cancellationToken);

            _logger.LogInformation(
                "Completed step {StepName}",
                step.Name);
        }

        session.Status = "Completed";

        await _repository.UpdateAsync(
            session,
            cancellationToken);

        _logger.LogInformation(
            "Workflow completed");
    }

    public async Task PauseAsync(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var session =
            await _repository.GetAsync(
                sessionId,
                cancellationToken);

        if (session is null)
        {
            return;
        }

        session.Status = "Paused";

        await _repository.UpdateAsync(
            session,
            cancellationToken);
    }

    public async Task CancelAsync(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var session =
            await _repository.GetAsync(
                sessionId,
                cancellationToken);

        if (session is null)
        {
            return;
        }

        session.Status = "Cancelled";

        await _repository.UpdateAsync(
            session,
            cancellationToken);
    }
}
using ITMartin.Media.Application.Abstractions.Events;
using ITMartin.Media.Application.Abstractions.Orchestration;
using ITMartin.Media.Application.Abstractions.Scanning;
using ITMartin.Media.Application.Events.Scanning;
using ITMartin.Media.Application.Models.Scan;
using ITMartin.Media.Application.Models.Scanning;
using ITMartin.Media.Application.Pipelines.Package1.Orchestration;
using ITMartin.Media.Contracts.Contracts.Runtime.Persistence;

namespace ITMartin.Media.Application.Pipelines.Package1;

public sealed class Package1WorkflowOrchestrator
    : IScanOrchestrator
{
    private readonly IWorkflowInstanceStore _workflowInstanceStore;
    private readonly Package1WorkflowDefinition _workflowDefinition;
    private readonly IScanSessionRepository _repository;
    private readonly IEventPublisher _eventPublisher;

    public Package1WorkflowOrchestrator(
        IWorkflowInstanceStore workflowInstanceStore,
        Package1WorkflowDefinition workflowDefinition,
        IScanSessionRepository repository,
        IEventPublisher eventPublisher)
    {
        _workflowInstanceStore =
            workflowInstanceStore;

        _workflowDefinition =
            workflowDefinition;

        _repository =
            repository;

        _eventPublisher =
            eventPublisher;
    }

    public async Task<Guid> StartAsync(
        StartScanRequest request,
        CancellationToken cancellationToken)
    {
        var session = new ScanSession
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
        var session =
            await _repository.GetAsync(
                sessionId,
                cancellationToken);

        if (session is null)
        {
            return;
        }

        session.Status = "Running";

        await _repository.UpdateAsync(
            session,
            cancellationToken);
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
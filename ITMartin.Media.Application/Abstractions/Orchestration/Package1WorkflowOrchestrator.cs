using ITMartin.Media.Application.Abstractions.Events;
using ITMartin.Media.Application.Abstractions.Scanning;
using ITMartin.Media.Application.Events.Scanning;
using ITMartin.Media.Application.Models.Scan;
using ITMartin.Media.Application.Models.Scanning;
using ITMartin.Media.Application.Pipelines.Package1.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;

namespace ITMartin.Media.Application.Abstractions.Orchestration;

public sealed class Package1WorkflowOrchestrator : IScanOrchestrator
{
    private readonly IWorkflowExecutor _workflowExecutor;
    private readonly Package1WorkflowDefinition _workflowDefinition;
    private readonly IPackage1ManifestStore _manifestStore;
    private readonly Package1ManifestBuilder _manifestBuilder;
    private readonly IScanSessionRepository _repository;
    private readonly IEventPublisher _eventPublisher;

    public Package1WorkflowOrchestrator(
        IWorkflowExecutor workflowExecutor,
        Package1WorkflowDefinition workflowDefinition,
        IPackage1ManifestStore manifestStore,
        Package1ManifestBuilder manifestBuilder, IScanSessionRepository repository, IEventPublisher eventPublisher)
    {
        _workflowExecutor = workflowExecutor;
        _workflowDefinition = workflowDefinition;
        _manifestStore = manifestStore;
        _manifestBuilder = manifestBuilder;
        _repository = repository;
        _eventPublisher = eventPublisher;
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

        await _repository.CreateAsync(session, cancellationToken);

        await _eventPublisher.PublishAsync(
            
            new ScanStartedEvent(
                Guid.NewGuid(),
                session.Id,
                request.RootPath,
                DateTimeOffset.UtcNow),
            cancellationToken);
        var context =
            new WorkflowExecutionContext<Package1WorkflowState>
            {
                WorkflowId = session.Id,
                WorkflowName = _workflowDefinition.Name,
                State =
                    new Package1WorkflowState
                    {
                        RootPath = request.RootPath
                    },
                CancellationToken = cancellationToken
            };

        await _workflowExecutor.ExecuteAsync(
            _workflowDefinition,
            context,
            cancellationToken);

        var manifest =
            _manifestBuilder.Build(
                session.Id,
                context.State);

        await _manifestStore.SaveAsync(
            manifest,
            cancellationToken);
        return session.Id;
    }

    public async Task ResumeAsync(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var session = await _repository.GetAsync(
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
        var session = await _repository.GetAsync(
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
        var session = await _repository.GetAsync(
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
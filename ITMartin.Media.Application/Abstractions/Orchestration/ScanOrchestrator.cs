using ITMartin.Media.Application.Abstractions.Events;
using ITMartin.Media.Application.Abstractions.Scanning;
using ITMartin.Media.Application.Events.Scanning;
using ITMartin.Media.Application.Models.Scan;
using ITMartin.Media.Application.Models.Scanning;

namespace ITMartin.Media.Application.Abstractions.Orchestration;

public sealed class ScanOrchestrator : IScanOrchestrator
{
    private readonly IScanSessionRepository _repository;
    private readonly IEventPublisher _eventPublisher;

    public ScanOrchestrator(
        IScanSessionRepository repository,
        IEventPublisher eventPublisher)
    {
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
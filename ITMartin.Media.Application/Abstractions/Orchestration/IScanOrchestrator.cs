using ITMartin.Media.Application.Models.Scan;

namespace ITMartin.Media.Application.Abstractions.Orchestration;

public interface IScanOrchestrator
{
    Task<Guid> StartAsync(StartScanRequest request, CancellationToken cancellationToken);

    Task ResumeAsync(Guid sessionId, CancellationToken cancellationToken);

    Task PauseAsync(Guid sessionId, CancellationToken cancellationToken);

    Task CancelAsync(Guid sessionId, CancellationToken cancellationToken);
}
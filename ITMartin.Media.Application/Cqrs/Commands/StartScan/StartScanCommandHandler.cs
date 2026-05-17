using ITMartin.Media.Application.Abstractions.Orchestration;
using ITMartin.Media.Application.Models.Scan;

namespace ITMartin.Media.Application.CQRS.Commands.StartScan;

public sealed class StartScanCommandHandler
    : ICommandHandler<StartScanCommand>
{
    private readonly IScanOrchestrator _orchestrator;

    public StartScanCommandHandler(
        IScanOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    public async Task HandleAsync(
        StartScanCommand command,
        CancellationToken cancellationToken)
    {
        await _orchestrator.StartAsync(
            new StartScanRequest(
                command.RootPath,
                command.Recursive,
                true,
                true,
                "Package1"),
            cancellationToken);
    }
}
using ITMartin.Media.Application.Pipelines.Package2.Orchestration;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Requests;

namespace ITMartin.Media.Application.Pipelines.Package2.Clients;

public sealed class Package2Client
    : IPackage2Client
{
    private readonly Package2WorkflowOrchestrator
        _orchestrator;

    public Package2Client(
        Package2WorkflowOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    public async Task StartAsync(
        StartPackage2Request request,
        CancellationToken cancellationToken)
    {
        await _orchestrator.RunAsync(
            request,
            cancellationToken);
    }
}
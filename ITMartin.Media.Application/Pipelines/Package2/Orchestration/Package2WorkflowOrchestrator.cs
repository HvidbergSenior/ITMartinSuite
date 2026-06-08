using ITMartin.Media.Application.Pipelines.Package2.Services;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Requests.Package2;

namespace ITMartin.Media.Application.Pipelines.Package2.Orchestration;

public sealed class Package2WorkflowOrchestrator
{
    private readonly Package2WorkflowFactory
        _factory;

    private readonly Package1ManifestLoader
        _manifestLoader;

    public Package2WorkflowOrchestrator(
        Package2WorkflowFactory factory,
        Package1ManifestLoader manifestLoader)
    {
        _factory = factory;
        _manifestLoader = manifestLoader;
    }

    public async Task<Package2WorkflowStartResult>
        StartAsync(
            StartPackage2Request request,
            CancellationToken cancellationToken)
    {
        var manifest =
            await _manifestLoader.LoadAsync(
                request.SourceLibraryPath,
                cancellationToken);

        var state =
            _factory.Create(
                manifest,
                request);

        return new Package2WorkflowStartResult(
            Guid.NewGuid(),
            state);
    }
}
using ITMartin.Media.Contracts.Contracts.Runtime.Requests.Package1;

namespace ITMartin.Media.Infrastructure.BackgroundJobs;

public sealed class Package1WorkflowJob
{
    public required StartPackage1Request
        Request { get; init; }
}
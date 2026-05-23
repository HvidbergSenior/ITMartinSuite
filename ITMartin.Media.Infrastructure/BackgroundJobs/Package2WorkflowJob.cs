using ITMartin.Media.Contracts.Contracts.Runtime.Requests;
using ITMartin.Media.Contracts.Contracts.Runtime.Requests.Package2;

namespace ITMartin.Media.Infrastructure.BackgroundJobs;

public sealed class Package2WorkflowJob
{
    public required StartPackage2Request
        Request { get; init; }
}
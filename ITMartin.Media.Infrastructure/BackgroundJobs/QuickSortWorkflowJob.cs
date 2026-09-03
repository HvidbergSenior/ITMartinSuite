using ITMartin.Media.Contracts.Contracts.Runtime.Requests.QuickSort;

namespace ITMartin.Media.Infrastructure.BackgroundJobs;

public sealed class QuickSortWorkflowJob
{
    public required StartQuickSortRequest
        Request { get; init; }
}
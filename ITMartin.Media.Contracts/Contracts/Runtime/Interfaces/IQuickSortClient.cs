using ITMartin.Media.Contracts.Contracts.Runtime.Requests;
using ITMartin.Media.Contracts.Contracts.Runtime.Requests.QuickSort;

namespace ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;

public interface IQuickSortClient
{
    Task<Guid> StartAsync(
        StartQuickSortRequest request,
        CancellationToken cancellationToken);
}
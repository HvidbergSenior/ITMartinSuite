using ITMartin.Media.Application.Pipelines.QuickSort.Models.Scanning;

namespace ITMartin.Media.Application.Abstractions.WebSockets;

public interface IScanProgressNotifier
{
    Task NotifyAsync(
        ScanProgressModel progress,
        CancellationToken cancellationToken);
}
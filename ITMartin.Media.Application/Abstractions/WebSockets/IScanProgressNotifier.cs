using ITMartin.Media.Application.Models.Scan;

namespace ITMartin.Media.Application.Abstractions.WebSockets;

public interface IScanProgressNotifier
{
    Task NotifyAsync(
        ScanProgressModel progress,
        CancellationToken cancellationToken);
}
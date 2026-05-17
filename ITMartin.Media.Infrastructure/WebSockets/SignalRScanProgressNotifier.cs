using ITMartin.Media.Application.Abstractions.WebSockets;
using ITMartin.Media.Application.Models.Scan;
using Microsoft.AspNetCore.SignalR;

namespace ITMartin.Media.Infrastructure.WebSockets;

public sealed class SignalRScanProgressNotifier
    : IScanProgressNotifier
{
    private readonly IHubContext<ScanProgressHub> _hubContext;

    public SignalRScanProgressNotifier(
        IHubContext<ScanProgressHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public Task NotifyAsync(
        ScanProgressModel progress,
        CancellationToken cancellationToken)
    {
        return _hubContext.Clients.All.SendAsync(
            "scan-progress",
            progress,
            cancellationToken);
    }
}
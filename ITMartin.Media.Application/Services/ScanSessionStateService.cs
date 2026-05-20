using ITMartin.Media.Application.Models.Scanning;

namespace ITMartin.Media.Application.Services;

public sealed class ScanSessionStateService
{
    private static readonly Dictionary<Guid,
            ScanSessionState>
        Sessions = [];

    public Task SaveAsync(
        ScanSessionState state,
        CancellationToken cancellationToken)
    {
        Sessions[state.SessionId] = state;

        return Task.CompletedTask;
    }

    public Task<ScanSessionState?> GetAsync(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        Sessions.TryGetValue(
            sessionId,
            out var state);

        return Task.FromResult(state);
    }
}
namespace ITMartinLadestander.Server.Services;

public enum OutletState { Unknown, Free, Connected, Charging, Finished, Offline }

// One outlet right now, as the chargers report it.
public sealed record OutletLive(int Outlet, OutletState State, double Kw, double SessionKwh, DateTime? SinceDk, string Token);

// One finished charging session, as the chargers report it (times in Danish local time).
public sealed record FeedSession(string ExternalId, int Outlet, DateTime StartDk, DateTime EndDk, double Kwh, string Token);

// Where live status and finished sessions come from: the Zaptec API when the association's login is configured,
// otherwise the simulator, so the app can be built and shown before the chargers exist.
public interface IChargerFeed
{
    string Name { get; }
    bool IsSimulated { get; }
    Task<List<OutletLive>> LiveAsync(ForeningData f, CancellationToken ct);
    Task<List<FeedSession>> SessionsAsync(ForeningData f, DateTime fromDk, DateTime toDk, CancellationToken ct);
}

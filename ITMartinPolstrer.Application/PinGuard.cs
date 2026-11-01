using System.Collections.Concurrent;

namespace ITMartinPolstrer.Application;

public enum PinResult { Ok, Wrong, Locked }

/// <summary>
/// The single-PIN login with a brake: five wrong tries from one address lock that address for 15 minutes
/// (2026-11-01: someone tried 17 PINs in 1.5 minutes). A right PIN clears the count. Singleton, kept in memory.
/// </summary>
public sealed class PinGuard(TimeProvider clock)
{
    public const int MaxWrongTries = 5;
    public static readonly TimeSpan LockTime = TimeSpan.FromMinutes(15);
    private readonly ConcurrentDictionary<string, (int Count, DateTime First)> _wrong = new();

    public PinResult Check(string? typed, string pin, string address)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        if (_wrong.TryGetValue(address, out var w) && now - w.First >= LockTime) { _wrong.TryRemove(address, out _); w = default; }
        if (w.Count >= MaxWrongTries) return PinResult.Locked;
        if (pin.Length > 0 && (typed ?? "").Trim() == pin) { _wrong.TryRemove(address, out _); return PinResult.Ok; }
        var after = _wrong.AddOrUpdate(address, _ => (1, now), (_, old) => (old.Count + 1, old.First));
        return after.Count >= MaxWrongTries ? PinResult.Locked : PinResult.Wrong;
    }
}

using System.Security.Cryptography;
using System.Text;

namespace ITMartinSnak.Domain;

// 2026-11-12 (family room Skejby, user: "only the 5 members", "once you choose your name, no one can enter with that - no need for a
// password then?"): in a family room (named people, not a course group) each name is LOCKED to the phone that took it first.
// Another phone that taps the same name waits until the room's admin approves it ("Godkend") - a new phone, or an iPhone's
// home-screen icon, which has its own storage. Nobody else gets past the door: no free names, no typed names.
// The phone holds a random token in a cookie; the room keeps only its hash.
public sealed record Device(string Hash, string Kind, DateTime At);

public sealed record PendingDevice(string Name, string Hash, string Kind, DateTime At)
{
    /// <summary>A short id for the approve button (never the hash itself on the page).</summary>
    public string Id => Hash.Length > 10 ? Hash[..10] : Hash;
}

public enum DoorResult { In, Waiting, NotOnList }

public static class DoorRules
{
    public const int MaxDevicesPerName = 6, MaxPending = 20;

    /// <summary>Family rooms lock names to phones; course groups and customer rooms keep the old open door.</summary>
    public static bool Locked(Room r) => !r.Gruppe && (r.Members?.Count ?? 0) > 0;

    public static string NewToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(18)).Replace('+', '-').Replace('/', '_');

    public static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("snak-dør:" + token)));

    public static string? MemberName(Room r, string? name)
    {
        var n = GroupRules.CleanName(name);
        return (r.Members ?? []).FirstOrDefault(m => string.Equals(m, n, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>The name this phone holds in the room, or null.</summary>
    public static string? NameOf(Room r, string? tokenHash) =>
        tokenHash is null ? null : r.Claims?.FirstOrDefault(kv => kv.Value.Any(d => d.Hash == tokenHash)).Key;

    /// <summary>A phone taps a name: the first phone takes it; another phone waits for the admin's approval.</summary>
    public static (Room Room, DoorResult Result) Knock(Room r, string? name, string tokenHash, string? kind, DateTime nowUtc)
    {
        if (MemberName(r, name) is not { } n) return (r, DoorResult.NotOnList);
        var claims = Copy(r.Claims);
        var pending = (r.Pending ?? []).ToList();
        // This phone already holds a name: it keeps it (one phone = one person).
        if (NameOf(r, tokenHash) is { } held) return (r, string.Equals(held, n, StringComparison.OrdinalIgnoreCase) ? DoorResult.In : DoorResult.Waiting);
        var k = Kind(kind);
        if (!claims.TryGetValue(n, out var devices) || devices.Count == 0)
        {
            claims[n] = [new Device(tokenHash, k, nowUtc)];
            pending.RemoveAll(p => p.Hash == tokenHash);
            return (r with { Claims = claims, Pending = pending }, DoorResult.In);
        }
        if (!pending.Any(p => p.Hash == tokenHash))
        {
            pending.RemoveAll(p => p.Hash == tokenHash);
            pending.Add(new PendingDevice(n, tokenHash, k, nowUtc));
            if (pending.Count > MaxPending) pending.RemoveRange(0, pending.Count - MaxPending);
        }
        return (r with { Pending = pending }, DoorResult.Waiting);
    }

    /// <summary>The admin lets a waiting phone in under the name it asked for.</summary>
    public static Room? Approve(Room r, string pendingId)
    {
        var p = (r.Pending ?? []).FirstOrDefault(x => x.Id == pendingId);
        if (p is null) return null;
        var claims = Copy(r.Claims);
        var list = claims.TryGetValue(p.Name, out var l) ? l : claims[p.Name] = [];
        if (list.Count >= MaxDevicesPerName) list.RemoveAt(0);
        list.Add(new Device(p.Hash, p.Kind, p.At));
        return r with { Claims = claims, Pending = (r.Pending ?? []).Where(x => x.Hash != p.Hash).ToList() };
    }

    /// <summary>The admin frees a name (wrong person took it, or a fresh start): every phone on it is out, the next tap takes it.</summary>
    public static Room Release(Room r, string? name)
    {
        var n = MemberName(r, name) ?? GroupRules.CleanName(name);
        var claims = Copy(r.Claims);
        claims.Remove(n);
        return r with { Claims = claims, Pending = (r.Pending ?? []).Where(p => !string.Equals(p.Name, n, StringComparison.OrdinalIgnoreCase)).ToList() };
    }

    /// <summary>The admin turns a waiting phone away.</summary>
    public static Room Reject(Room r, string pendingId) => r with { Pending = (r.Pending ?? []).Where(p => p.Id != pendingId).ToList() };

    private static string Kind(string? kind) => kind is "iphone" or "android" or "computer" ? kind : "";

    private static Dictionary<string, List<Device>> Copy(Dictionary<string, List<Device>>? c) =>
        (c ?? []).ToDictionary(kv => kv.Key, kv => kv.Value.ToList(), StringComparer.OrdinalIgnoreCase);
}

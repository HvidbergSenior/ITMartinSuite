using System.Security.Cryptography;
using System.Text;

namespace ITMartinMode.Domain;

/// <summary>Someone in the room right now. Token = their secret for publishing their own camera.</summary>
public sealed class Participant(string id, string name, string token, DateTimeOffset joinedAt)
{
    public string Id { get; } = id;
    public string Name { get; } = name;
    public string Token { get; } = token;
    public DateTimeOffset JoinedAt { get; } = joinedAt;
    public DateTimeOffset LastSeen { get; set; } = joinedAt;

    /// <summary>The MediaMTX path this person publishes to; the others play it.</summary>
    public string MediaPath(string slug) => $"mode-{slug}-{Id}";
}

/// <summary>A closed meeting room: the link (slug + key) is the door. Few people, nothing recorded.</summary>
public sealed class Room(string slug, string title, string key, Board board)
{
    public const int MaxPeople = 6;
    public static readonly TimeSpan Gone = TimeSpan.FromSeconds(40);

    private readonly List<Participant> _people = new();
    public string Slug { get; } = slug;
    public string Title { get; } = title;
    public Board Board { get; } = board;
    public IReadOnlyList<Participant> People => _people;

    /// <summary>The De 6 HV turns, while the group plays (not saved - a restart just means "Start spillet" again).</summary>
    public Game? Game { get; private set; }

    public void StartGame(DateTimeOffset now) =>
        Game = new Game(_people.Select(p => new Seat(p.Id, p.Name)).ToList(), now);

    public void StopGame() => Game = null;

    public bool KeyOk(string? k) => k is not null && Same(k, key);

    public Participant Join(string? name, DateTimeOffset now)
    {
        var n = (name ?? "").Trim();
        if (n.Length == 0) throw new DomainException("Skriv dit navn.");
        if (n.Length > 40) n = n[..40];
        Sweep(now);
        if (_people.Count >= MaxPeople) throw new DomainException("Rummet er fuldt.");
        var p = new Participant(RandomId(8), n, RandomId(24), now);
        _people.Add(p);
        return p;
    }

    public Participant? Find(string? id, string? token) =>
        _people.FirstOrDefault(p => p.Id == id && token is not null && Same(token, p.Token));

    public bool Leave(string id) => _people.RemoveAll(p => p.Id == id) > 0;

    /// <summary>People whose page has not said hello for a while have left (closed tab, lost wifi).</summary>
    public bool Sweep(DateTimeOffset now) => _people.RemoveAll(p => now - p.LastSeen > Gone) > 0;

    private static bool Same(string a, string b) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(a), Encoding.UTF8.GetBytes(b));

    private static string RandomId(int len)
    {
        const string a = "abcdefghijkmnpqrstuvwxyz23456789";
        return new string(RandomNumberGenerator.GetBytes(len).Select(x => a[x % a.Length]).ToArray());
    }
}

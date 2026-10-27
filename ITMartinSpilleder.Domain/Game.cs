using System.Security.Cryptography;
using System.Text;

namespace ITMartinSpilleder.Domain;

// A spell or ability on a player's card: what it is called and what it does (user 2026-10-27: "what do your spells do?").
public sealed record Spell(string Name, string Text);

// One player at the table. Everyone in the game sees every card (user 2026-10-27: "an overview of players, names, races etc -
// so everybody can see all and do not forget it"). TokenHash is the player's login on their phone, never sent out.
// Hp/MaxHp are live on every screen (user 2026-10-27: "like my StarRealms app - when hitpoints drop the other guys can see it").
// Picture = version of the character's picture (0 = none), so phones fetch the new one after a change.
// Locked: once a player saves their sheet it is locked; only the leader can open it again (user 2026-10-27: "when save you can
// not edit anymore... only when dungeonmaster gives permission, for instance if you change weapons"). HP stays open - it is play.
// Gear = weapons and equipment, shown on the card with everything else ("all things on the sheet should be seen on your person").
public sealed record Player(string Id, string Name, string Character, string Race, string Role, int Hp, int MaxHp, string Notes,
    List<Spell> Spells, string TokenHash, DateTime JoinedAt, int Picture = 0, bool Locked = false, string Gear = "");

// A message at the table. To = the player ids it is for; empty = everyone. From = "leder" or a player id.
// Kind: "besked" or "terning" (a dice roll made by the server, so nobody can cheat).
public sealed record Note(long Id, string From, string FromName, List<string> To, string Text, string Kind, DateTime At);

// A game: one leader (Julius), a few players, the cards and the messages. JoinCode is for the players, LeaderCode lets the
// leader come back on another phone.
public sealed class Game
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string LeaderName { get; set; } = "";
    public string JoinCode { get; set; } = "";
    public string LeaderCode { get; set; } = "";
    public List<string> LeaderTokenHashes { get; set; } = [];
    public List<Player> Players { get; set; } = [];
    public List<Note> Notes { get; set; } = [];
    public DateTime CreatedAt { get; set; }
    public DateTime LastActive { get; set; }
}

public static class Viewer
{
    public const string Leader = "leder";
}

public static class GameRules
{
    public const int MaxTitle = 60, MaxName = 30, MaxField = 60, MaxNotes = 2000, MaxText = 2000, MaxNotesInGame = 3000,
        MaxPlayers = 12, MaxSpells = 40, MaxSpellName = 60, MaxSpellText = 3000;
    public const int MaxHpLimit = 999, MinHp = -99;
    public const int MaxPictureBytes = 600_000;
    public static readonly TimeSpan KeepIdle = TimeSpan.FromDays(120);

    // No 0/O/1/I/L - the code is read aloud at the table.
    private const string CodeChars = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    public static string Cut(string? s, int max)
    {
        var t = (s ?? "").Trim();
        return t.Length > max ? t[..max] : t;
    }

    public static string NewCode(int length) =>
        new(Enumerable.Range(0, length).Select(_ => CodeChars[RandomNumberGenerator.GetInt32(CodeChars.Length)]).ToArray());

    public static string NewToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(24));

    public static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    public static string CleanCode(string? code) => (code ?? "").Trim().ToUpperInvariant().Replace(" ", "").Replace("-", "");

    public static Game NewGame(string? title, string? leaderName, DateTime nowUtc, string leaderToken) => new()
    {
        Id = Guid.NewGuid().ToString("N")[..12],
        Title = Cut(title, MaxTitle) is { Length: > 0 } t ? t : "Vores eventyr",
        LeaderName = Cut(leaderName, MaxName) is { Length: > 0 } n ? n : "Spilleder",
        JoinCode = NewCode(6),
        LeaderCode = NewCode(8),
        LeaderTokenHashes = [Hash(leaderToken)],
        CreatedAt = nowUtc,
        LastActive = nowUtc,
    };

    /// <returns>The new player, or null when the name is missing or the table is full.</returns>
    public static Player? NewPlayer(Game g, string? name, string token, DateTime nowUtc)
    {
        var n = Cut(name, MaxName);
        if (n.Length == 0 || g.Players.Count >= MaxPlayers) return null;
        return new Player(Guid.NewGuid().ToString("N")[..8], n, "", "", "", 10, 10, "", [], Hash(token), nowUtc);
    }

    public static Player EditCard(Player p, string? name, string? character, string? race, string? role, int hp, int maxHp, string? notes,
        string? gear, IEnumerable<Spell>? spells) => p with
    {
        Name = Cut(name, MaxName) is { Length: > 0 } n ? n : p.Name,
        Character = Cut(character, MaxField),
        Race = Cut(race, MaxField),
        Role = Cut(role, MaxField),
        MaxHp = Math.Clamp(maxHp, 1, MaxHpLimit),
        Hp = Math.Clamp(hp, MinHp, Math.Clamp(maxHp, 1, MaxHpLimit)),
        Notes = Cut(notes, MaxNotes),
        Gear = Cut(gear, MaxNotes),
        Spells = (spells ?? [])
            .Select(s => new Spell(Cut(s.Name, MaxSpellName), Cut(s.Text, MaxSpellText)))
            .Where(s => s.Name.Length > 0)
            .Take(MaxSpells).ToList(),
    };

    /// <summary>The − / + buttons: HP goes up or down, never above MaxHp (below 0 is allowed - some games count that).</summary>
    public static Player ChangeHp(Player p, int delta) => p with { Hp = Math.Clamp(p.Hp + Math.Clamp(delta, -999, 999), MinHp, p.MaxHp) };

    /// <summary>The sheet (everything but HP): the leader always; a player only while the leader has it unlocked.</summary>
    public static bool MayEditSheet(Player p, string viewer) => viewer == Viewer.Leader || (viewer == p.Id && !p.Locked);

    /// <summary>A character picture must be a JPEG (the phone shrinks it before sending) and not too big.</summary>
    public static bool IsPicture(byte[] bytes) =>
        bytes.Length is > 100 and <= MaxPictureBytes && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF;

    /// <summary>Who is behind a token: "leder", a player id, or null.</summary>
    public static string? WhoIs(Game g, string? token)
    {
        if (string.IsNullOrEmpty(token)) return null;
        var h = Hash(token);
        if (g.LeaderTokenHashes.Contains(h)) return Viewer.Leader;
        return g.Players.FirstOrDefault(p => p.TokenHash == h)?.Id;
    }

    public static string NameOf(Game g, string viewer) =>
        viewer == Viewer.Leader ? g.LeaderName : g.Players.FirstOrDefault(p => p.Id == viewer)?.Name ?? "?";

    /// <summary>A secret message is seen by the leader, the sender and the ones it is for; To empty = everyone.</summary>
    public static bool CanSee(Note n, string viewer) =>
        viewer == Viewer.Leader || n.To.Count == 0 || n.From == viewer || n.To.Contains(viewer);

    /// <summary>
    /// The leader may write to everyone or to any of the players; a player to everyone or to the leader only
    /// (players do not whisper to each other behind the leader's back).
    /// </summary>
    public static Note? NewNote(Game g, string from, IEnumerable<string>? to, string? text, string kind, DateTime nowUtc)
    {
        var t = Cut(text, MaxText);
        if (t.Length == 0) return null;
        var ids = (to ?? []).Distinct().ToList();
        List<string> targets;
        // The leader may also write only to themselves (a hidden dice roll): To = ["leder"], which no player sees.
        if (from == Viewer.Leader)
            targets = ids.Count == 1 && ids[0] == Viewer.Leader ? [Viewer.Leader] : ids.Where(id => g.Players.Any(p => p.Id == id)).ToList();
        else targets = ids.Contains(Viewer.Leader) ? [Viewer.Leader] : [];
        // Picked players that have all left: do not turn a secret into a message for everyone.
        if (ids.Count > 0 && targets.Count == 0) return null;
        return new Note((g.Notes.Count == 0 ? 0 : g.Notes[^1].Id) + 1, from, NameOf(g, from), targets, t, kind, nowUtc);
    }

    public static void Trim(Game g)
    {
        if (g.Notes.Count > MaxNotesInGame) g.Notes.RemoveRange(0, g.Notes.Count - MaxNotesInGame);
    }

    /// <summary>"2d6", "d20", "1d8+2" -> the rolls and the total; null when it is not a dice text.</summary>
    public static (int[] Rolls, int Bonus, int Total)? Roll(string? dice, Func<int, int> random)
    {
        var m = System.Text.RegularExpressions.Regex.Match((dice ?? "").Trim().ToLowerInvariant(), @"^(\d{0,2})d(\d{1,3})([+-]\d{1,3})?$");
        if (!m.Success) return null;
        var count = m.Groups[1].Value.Length == 0 ? 1 : int.Parse(m.Groups[1].Value);
        var sides = int.Parse(m.Groups[2].Value);
        var bonus = m.Groups[3].Success ? int.Parse(m.Groups[3].Value) : 0;
        if (count is < 1 or > 20 || sides is < 2 or > 100) return null;
        var rolls = Enumerable.Range(0, count).Select(_ => random(sides) + 1).ToArray();
        return (rolls, bonus, rolls.Sum() + bonus);
    }
}

using System.Text.Json;
using System.Text.Json.Nodes;
using ITMartinStrategi.Application;
using ITMartinStrategi.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace ITMartinStrategi.Infrastructure;

/// <summary>The bird cards from wwwroot/games/wingspan-start.json (the Start helper's data). "Common Chaff" and
/// "Red-backed" are found too: every typed word must start a word of the name, in order; else the closest spelling.</summary>
public sealed class JsonWingspanCards : IWingspanCards
{
    private readonly List<WingspanBird> _birds;

    public JsonWingspanCards(string path)
    {
        var root = JsonNode.Parse(File.ReadAllText(path))!;
        _birds = root["birds"]!.AsArray().Select(b => new WingspanBird(
            (string)b!["name"]!, (string?)b["tier"] ?? "", (double?)b["score"] ?? 0,
            b["habitats"]!.AsArray().Select(h => (string)h!).ToList(),
            b["food"]!.AsObject().ToDictionary(f => f.Key, f => (int)f.Value!),
            (bool?)b["foodOr"] ?? false, (int?)b["points"] ?? 0, (int?)b["eggs"] ?? 0,
            (string?)b["nest"] ?? "", (string?)b["color"] ?? "", (string?)b["kind"] ?? "", (string?)b["power"] ?? "", (string?)b["why"] ?? ""))
            .ToList();
    }

    public IReadOnlyList<WingspanBird> All => _birds;

    public (WingspanBird? Bird, IReadOnlyList<string> Suggestions) Find(string name)
    {
        var typed = Words(name);
        if (typed.Length == 0) return (null, []);
        var exact = _birds.FirstOrDefault(b => string.Join(" ", Words(b.Name)) == string.Join(" ", typed));
        if (exact is not null) return (exact, []);
        var prefix = _birds.Where(b => StartsInOrder(Words(b.Name), typed)).ToList();
        if (prefix.Count == 1) return (prefix[0], []);
        if (prefix.Count > 1) return (null, prefix.Take(3).Select(b => b.Name).ToList());
        var joined = string.Join(" ", typed);
        var close = _birds.Select(b => (b, d: Distance(string.Join(" ", Words(b.Name)), joined))).OrderBy(x => x.d).Take(3).ToList();
        return close[0].d <= Math.Max(2, joined.Length / 5) ? (close[0].b, []) : (null, close.Select(x => x.b.Name).ToList());
    }

    private static string[] Words(string s) =>
        s.ToLowerInvariant().Replace('-', ' ').Replace("'", "").Split(' ', StringSplitOptions.RemoveEmptyEntries);

    // Each typed word starts one of the name's words, in the same order ("common chaff" -> "common chaffinch").
    private static bool StartsInOrder(string[] name, string[] typed)
    {
        var i = 0;
        foreach (var w in name)
            if (i < typed.Length && w.StartsWith(typed[i])) i++;
        return i == typed.Length;
    }

    private static int Distance(string a, string b)
    {
        var d = new int[a.Length + 1, b.Length + 1];
        for (var i = 0; i <= a.Length; i++) d[i, 0] = i;
        for (var j = 0; j <= b.Length; j++) d[0, j] = j;
        for (var i = 1; i <= a.Length; i++)
            for (var j = 1; j <= b.Length; j++)
                d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));
        return d[a.Length, b.Length];
    }
}

/// <summary>The challenge weeks in one JSON file on the data volume (DataDir/wingspan-challenges.json).</summary>
public sealed class JsonChallengeStore(string path) : IChallengeStore
{
    private static readonly JsonSerializerOptions Opts = new() { WriteIndented = true };
    private readonly object _lock = new();

    public IReadOnlyList<WingspanChallenge> All() { lock (_lock) return Load(); }

    public WingspanChallenge? Get(Guid id) { lock (_lock) return Load().FirstOrDefault(c => c.Id == id); }

    public void Save(WingspanChallenge challenge)
    {
        lock (_lock)
        {
            var all = Load();
            all.RemoveAll(c => c.Id == challenge.Id);
            all.Add(challenge);
            Write(all);
        }
    }

    public void Delete(Guid id)
    {
        lock (_lock)
        {
            var all = Load();
            all.RemoveAll(c => c.Id == id);
            Write(all);
        }
    }

    private List<WingspanChallenge> Load() =>
        File.Exists(path) ? JsonSerializer.Deserialize<List<WingspanChallenge>>(File.ReadAllText(path)) ?? [] : [];

    // Written next to the file first, then moved over it - a crash never leaves half a file.
    private void Write(List<WingspanChallenge> all)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(all, Opts));
        File.Move(path + ".tmp", path, overwrite: true);
    }
}

public static class DependencyInjection
{
    /// <param name="cardsFile">wwwroot/games/wingspan-start.json</param>
    /// <param name="dataDir">the data volume</param>
    public static IServiceCollection AddStrategiInfrastructure(this IServiceCollection services, string cardsFile, string dataDir)
    {
        services.AddSingleton<IWingspanCards>(_ => new JsonWingspanCards(cardsFile));
        services.AddSingleton<IChallengeStore>(_ => new JsonChallengeStore(Path.Combine(dataDir, "wingspan-challenges.json")));
        return services;
    }
}

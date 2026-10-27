using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;
using ITMartinCharacter.Application;
using ITMartinCharacter.Domain;
using ITMartinKunder.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ITMartinCharacter.Infrastructure;

public static class DependencyInjection
{
    /// <summary>JSON files, pictures, live feed, Kunder and the SRD spell lookup - files in the data folder (Character:DataDir, default /data).</summary>
    public static IServiceCollection AddCharacterInfrastructure(this IServiceCollection services, string dataDir, IConfiguration cfg)
    {
        // Kunder__Url + Kunder__ApiKey like every app; customers are checked as the free app "Spilleder" (Game Master's family).
        services.AddKunderClient(cfg, dataDir);
        services.AddSingleton<ICustomerCheck, KunderCustomerCheck>();
        services.AddSingleton<IFigureStore>(new JsonFigureStore(Path.Combine(dataDir, "figurer")));
        services.AddSingleton<IPictureStore>(new FilePictureStore(Path.Combine(dataDir, "billeder")));
        services.AddSingleton<ILiveFeed, SseLiveFeed>();
        services.AddHttpClient<ISpellBook, SrdSpellBook>(c =>
        {
            c.BaseAddress = new Uri("https://www.dnd5eapi.co/");
            c.Timeout = TimeSpan.FromSeconds(10);
        });
        return services;
    }
}

/// <summary>The kundenummer check against Kunder; Kunder's answers are Danish, the app is English, so they are put in English here.</summary>
public sealed class KunderCustomerCheck(IServiceProvider sp) : ICustomerCheck
{
    // Character belongs with Game Master: the same free app in Kunder's list (kunder.itmartin.dk/gratis-apps).
    public const string App = "Spilleder";

    public async Task<(bool Ok, string Name, string Error)> CheckAsync(string customerNo, CancellationToken ct)
    {
        var r = await sp.GetRequiredService<KunderClient>().CheckCodeAsync(customerNo, App, ct);
        if (r.Ok) return (true, r.Name, "");
        var e = r.Error;
        return (false, "",
            e.Contains("kender vi ikke") ? "We don't know that customer number. Check it, or sign up for free at itmartin.dk." :
            e.Contains("afmeldt") ? "That customer number is unsubscribed. Sign up again at itmartin.dk and it works again." :
            e.Contains("gratis apps") ? "Character is not open for customers yet. Ask ITMartin." :
            e.Contains("8 cifre") ? "A customer number has 8 digits – you find it in the mail from ITMartin." :
            "We can't check customer numbers right now – try again in a few minutes.");
    }
}

/// <summary>One JSON file per figure. Written via a temp file, so a crash never leaves half a file.</summary>
public sealed class JsonFigureStore : IFigureStore
{
    private readonly string _dir;

    public JsonFigureStore(string dir)
    {
        _dir = dir;
        Directory.CreateDirectory(dir);
    }

    private string FileOf(string id) => Path.Combine(_dir, id + ".json");

    public List<Figure> LoadAll()
    {
        var list = new List<Figure>();
        foreach (var f in Directory.GetFiles(_dir, "*.json"))
            try { if (JsonSerializer.Deserialize<Figure>(File.ReadAllText(f)) is { } g) list.Add(g); } catch { }
        return list;
    }

    public void Save(Figure figure)
    {
        var file = FileOf(figure.Id);
        File.WriteAllText(file + ".tmp", JsonSerializer.Serialize(figure));
        File.Move(file + ".tmp", file, true);
    }

    public void Delete(string figureId)
    {
        if (File.Exists(FileOf(figureId))) File.Delete(FileOf(figureId));
    }
}

/// <summary>The character pictures: billeder/(figure).jpg. Ids are our own hex ids, never user text.</summary>
public sealed class FilePictureStore(string dir) : IPictureStore
{
    private static bool Safe(string id) => id.Length is > 0 and <= 40 && id.All(char.IsAsciiLetterOrDigit);
    private string? FileOf(string id) => Safe(id) ? Path.Combine(dir, id + ".jpg") : null;

    public void Save(string figureId, byte[] jpeg)
    {
        if (FileOf(figureId) is not { } f) return;
        Directory.CreateDirectory(dir);
        File.WriteAllBytes(f + ".tmp", jpeg);
        File.Move(f + ".tmp", f, true);
    }

    public byte[]? Load(string figureId) => FileOf(figureId) is { } f && File.Exists(f) ? File.ReadAllBytes(f) : null;

    public void Delete(string figureId)
    {
        if (FileOf(figureId) is { } f && File.Exists(f)) File.Delete(f);
    }
}

/// <summary>Server-sent events, per figure and viewer, so the game master's own notes only reach the game master.</summary>
public sealed class SseLiveFeed : ILiveFeed
{
    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    private readonly ConcurrentDictionary<Guid, (string Figure, string Viewer, Channel<string> Ch)> _listeners = new();

    public (Guid Id, ChannelReader<string> Reader) Join(string figureId, string viewer)
    {
        var ch = Channel.CreateBounded<string>(new BoundedChannelOptions(200) { FullMode = BoundedChannelFullMode.DropOldest });
        var id = Guid.NewGuid();
        _listeners[id] = (figureId, viewer, ch);
        return (id, ch.Reader);
    }

    public void Leave(Guid id) => _listeners.TryRemove(id, out _);

    public void Send(string figureId, string kind, object data, Func<string, bool>? to = null)
    {
        var line = $"event: {kind}\ndata: {JsonSerializer.Serialize(data, WebJson)}\n\n";
        foreach (var l in _listeners.Values)
            if (l.Figure == figureId && (to is null || to(l.Viewer))) l.Ch.Writer.TryWrite(line);
    }
}

/// <summary>
/// The free D&amp;D 5e SRD spells from dnd5eapi.co (SRD 5.1, CC-BY-4.0, Wizards of the Coast). The list of names is fetched
/// once and kept; a spell's text is kept after the first look-up.
/// </summary>
public sealed class SrdSpellBook(HttpClient http) : ISpellBook
{
    private static IReadOnlyList<SpellHit>? _all;
    private static readonly ConcurrentDictionary<string, Spell> Cache = new();

    private sealed record ListResult([property: JsonPropertyName("results")] List<Item> Results);
    private sealed record Item([property: JsonPropertyName("index")] string Index, [property: JsonPropertyName("name")] string Name);
    private sealed record Detail(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("desc")] List<string>? Desc,
        [property: JsonPropertyName("higher_level")] List<string>? HigherLevel,
        [property: JsonPropertyName("level")] int Level,
        [property: JsonPropertyName("range")] string? Range,
        [property: JsonPropertyName("duration")] string? Duration,
        [property: JsonPropertyName("casting_time")] string? CastingTime);

    public async Task<IReadOnlyList<SpellHit>> SearchAsync(string text, CancellationToken ct)
    {
        var t = (text ?? "").Trim();
        if (t.Length < 2) return [];
        _all ??= (await http.GetFromJsonAsync<ListResult>("api/2014/spells", ct))?.Results
            .Select(i => new SpellHit(i.Index, i.Name)).ToList() ?? [];
        return _all.Where(s => s.Name.Contains(t, StringComparison.OrdinalIgnoreCase))
            .OrderBy(s => !s.Name.StartsWith(t, StringComparison.OrdinalIgnoreCase)).ThenBy(s => s.Name).Take(12).ToList();
    }

    public async Task<Spell?> GetAsync(string index, CancellationToken ct)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(index ?? "", "^[a-z0-9-]{1,60}$")) return null;
        if (Cache.TryGetValue(index!, out var hit)) return hit;
        var d = await http.GetFromJsonAsync<Detail>("api/2014/spells/" + index, ct);
        if (d is null) return null;
        var head = $"{(d.Level == 0 ? "Cantrip" : "Level " + d.Level)} · {d.CastingTime} · {d.Range} · {d.Duration}";
        var text = head + "\n\n" + string.Join("\n\n", d.Desc ?? []) +
                   (d.HigherLevel is { Count: > 0 } h ? "\n\nAt higher levels: " + string.Join(" ", h) : "");
        var s = new Spell(d.Name, text);
        Cache[index!] = s;
        return s;
    }
}

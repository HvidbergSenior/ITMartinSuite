using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;
using ITMartinSpilleder.Application;
using ITMartinKunder.Client;
using ITMartinSpilleder.Domain;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WebPush;

namespace ITMartinSpilleder.Infrastructure;

public static class DependencyInjection
{
    /// <summary>JSON files, live feed, WebPush and the SRD spell lookup - files in the data folder (Spilleder:DataDir, default /data).</summary>
    public static IServiceCollection AddSpillederInfrastructure(this IServiceCollection services, string dataDir, IConfiguration cfg)
    {
        // Kunder__Url + Kunder__ApiKey like every app; customers are checked as app "Game Master" (the free app "Spilleder" in Kunder).
        services.AddKunderClient(cfg, dataDir);
        services.AddSingleton<ICustomerCheck, KunderCustomerCheck>();
        services.AddSingleton<IGameStore>(new JsonGameStore(Path.Combine(dataDir, "spil")));
        services.AddSingleton<IPictureStore>(new FilePictureStore(Path.Combine(dataDir, "billeder")));
        services.AddSingleton<ILiveFeed, SseLiveFeed>();
        services.AddSingleton<IPushSender>(sp => new WebPushSender(dataDir, sp.GetRequiredService<ILogger<WebPushSender>>()));
        // Character (character.itmartin.dk) on the same Docker network; Character__ApiKey is the same in both apps (magic.env).
        services.AddHttpClient<ICharacterSource, HttpCharacterSource>(c =>
        {
            c.BaseAddress = new Uri(cfg["Character:Url"] is { Length: > 0 } u ? u : "http://character-web:8080/");
            c.Timeout = TimeSpan.FromSeconds(10);
            c.DefaultRequestHeaders.Add("X-Api-Key", cfg["Character:ApiKey"] ?? "");
        });
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
    // The name Martin gave the app in Kunder's free-apps list (kunder.itmartin.dk/gratis-apps).
    public const string App = "Spilleder";

    public async Task<(bool Ok, string Name, string Error)> CheckAsync(string customerNo, CancellationToken ct)
    {
        var r = await sp.GetRequiredService<KunderClient>().CheckCodeAsync(customerNo, App, ct);
        if (r.Ok) return (true, r.Name, "");
        var e = r.Error;
        return (false, "",
            e.Contains("kender vi ikke") ? "We don't know that customer number. Check it, or sign up for free at itmartin.dk." :
            e.Contains("afmeldt") ? "That customer number is unsubscribed. Sign up again at itmartin.dk and it works again." :
            e.Contains("gratis apps") ? "Game Master is not open for customers yet. Ask ITMartin." :
            e.Contains("8 cifre") ? "A customer number has 8 digits – you find it in the mail from ITMartin." :
            "We can't check customer numbers right now – try again in a few minutes.");
    }
}

/// <summary>One JSON file per game. Written via a temp file, so a crash never leaves half a file.</summary>
public sealed class JsonGameStore : IGameStore
{
    private readonly string _dir;

    public JsonGameStore(string dir)
    {
        _dir = dir;
        Directory.CreateDirectory(dir);
    }

    private string FileOf(string id) => Path.Combine(_dir, id + ".json");

    public List<Game> LoadAll()
    {
        var list = new List<Game>();
        foreach (var f in Directory.GetFiles(_dir, "*.json"))
            try { if (JsonSerializer.Deserialize<Game>(File.ReadAllText(f)) is { } g) list.Add(g); } catch { }
        return list;
    }

    public void Save(Game game)
    {
        var file = FileOf(game.Id);
        File.WriteAllText(file + ".tmp", JsonSerializer.Serialize(game));
        File.Move(file + ".tmp", file, true);
    }

    public void Delete(string gameId)
    {
        if (File.Exists(FileOf(gameId))) File.Delete(FileOf(gameId));
    }
}

/// <summary>The character pictures: billeder/(game)/(player).jpg. Ids are our own hex ids, never user text.</summary>
public sealed class FilePictureStore(string dir) : IPictureStore
{
    private static bool Safe(string id) => id.Length is > 0 and <= 40 && id.All(char.IsAsciiLetterOrDigit);
    private string? FileOf(string game, string player) => Safe(game) && Safe(player) ? Path.Combine(dir, game, player + ".jpg") : null;

    public void Save(string gameId, string playerId, byte[] jpeg)
    {
        if (FileOf(gameId, playerId) is not { } f) return;
        Directory.CreateDirectory(Path.GetDirectoryName(f)!);
        File.WriteAllBytes(f + ".tmp", jpeg);
        File.Move(f + ".tmp", f, true);
    }

    public byte[]? Load(string gameId, string playerId) =>
        FileOf(gameId, playerId) is { } f && File.Exists(f) ? File.ReadAllBytes(f) : null;

    public void Delete(string gameId, string? playerId = null)
    {
        if (playerId is not null) { if (FileOf(gameId, playerId) is { } f && File.Exists(f)) File.Delete(f); return; }
        if (Safe(gameId) && Directory.Exists(Path.Combine(dir, gameId))) Directory.Delete(Path.Combine(dir, gameId), true);
    }
}

/// <summary>Server-sent events, per game and viewer, so a secret message only reaches the ones who may see it.</summary>
public sealed class SseLiveFeed : ILiveFeed
{
    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    private readonly ConcurrentDictionary<Guid, (string Game, string Viewer, Channel<string> Ch)> _listeners = new();

    public (Guid Id, ChannelReader<string> Reader) Join(string gameId, string viewer)
    {
        var ch = Channel.CreateBounded<string>(new BoundedChannelOptions(200) { FullMode = BoundedChannelFullMode.DropOldest });
        var id = Guid.NewGuid();
        _listeners[id] = (gameId, viewer, ch);
        return (id, ch.Reader);
    }

    public void Leave(Guid id) => _listeners.TryRemove(id, out _);

    public void Send(string gameId, string kind, object data, Func<string, bool>? to = null)
    {
        var line = $"event: {kind}\ndata: {JsonSerializer.Serialize(data, WebJson)}\n\n";
        foreach (var l in _listeners.Values)
            if (l.Game == gameId && (to is null || to(l.Viewer))) l.Ch.Writer.TryWrite(line);
    }
}

/// <summary>
/// Push to the phones, per game and viewer. Keys are made on first start and kept in the data folder, so phones stay
/// subscribed across deploys. A phone that is gone (410/404) is dropped.
/// </summary>
public sealed class WebPushSender : IPushSender
{
    private readonly string _keysFile, _subsFile;
    private readonly object _gate = new();
    private readonly VapidDetails _vapid;
    private readonly WebPushClient _client = new();
    private readonly ILogger<WebPushSender> _log;
    private List<Subscriber> _subs = [];

    public sealed record Subscriber(string Game, string Viewer, string Endpoint, string P256dh, string Auth, DateTime AddedAt);
    private sealed record VapidKeys(string PublicKey, string PrivateKey);

    public WebPushSender(string dataDir, ILogger<WebPushSender> log)
    {
        _log = log;
        Directory.CreateDirectory(dataDir);
        _keysFile = Path.Combine(dataDir, "vapid.json");
        _subsFile = Path.Combine(dataDir, "push-subscribers.json");
        VapidKeys k;
        if (File.Exists(_keysFile)) k = JsonSerializer.Deserialize<VapidKeys>(File.ReadAllText(_keysFile))!;
        else
        {
            var g = VapidHelper.GenerateVapidKeys();
            k = new VapidKeys(g.PublicKey, g.PrivateKey);
            File.WriteAllText(_keysFile, JsonSerializer.Serialize(k));
        }
        _vapid = new VapidDetails("mailto:ITMartin@Mensa.dk", k.PublicKey, k.PrivateKey);
        if (File.Exists(_subsFile))
            try { _subs = JsonSerializer.Deserialize<List<Subscriber>>(File.ReadAllText(_subsFile)) ?? []; } catch { }
    }

    public string PublicKey => _vapid.PublicKey;

    public void Subscribe(string gameId, string viewer, string endpoint, string p256dh, string auth)
    {
        lock (_gate)
        {
            _subs.RemoveAll(x => x.Endpoint == endpoint);
            _subs.Add(new Subscriber(gameId, viewer, endpoint, p256dh, auth, DateTime.UtcNow));
            Save();
        }
    }

    public void Unsubscribe(string endpoint)
    {
        lock (_gate) { _subs.RemoveAll(x => x.Endpoint == endpoint); Save(); }
    }

    private void Save() => File.WriteAllText(_subsFile, JsonSerializer.Serialize(_subs));

    public void Send(string gameId, Func<string, bool> to, string title, string body)
    {
        List<Subscriber> targets;
        lock (_gate) targets = _subs.Where(s => s.Game == gameId && to(s.Viewer)).ToList();
        if (targets.Count == 0) return;
        if (body.Length > 140) body = body[..140] + "…";
        var payload = JsonSerializer.Serialize(new { title, body, url = "/", tag = "spilleder" });
        _ = Task.Run(async () =>
        {
            foreach (var s in targets)
            {
                try
                {
                    await _client.SendNotificationAsync(new PushSubscription(s.Endpoint, s.P256dh, s.Auth), payload, _vapid);
                }
                catch (WebPushException ex) when (ex.StatusCode is System.Net.HttpStatusCode.Gone or System.Net.HttpStatusCode.NotFound)
                {
                    Unsubscribe(s.Endpoint);
                }
                catch (Exception ex)
                {
                    _log.LogWarning("Spilleder push failed: {Error}", ex.Message);
                }
            }
        });
    }
}

/// <summary>The ready figures in Character, server to server with the shared key.</summary>
public sealed class HttpCharacterSource(HttpClient http, ILogger<HttpCharacterSource> log) : ICharacterSource
{
    private sealed record SpellDto(string? Name, string? Text);
    private sealed record FigureDto(string FigureCode, string? Name, string? People, string? Class, int Hp, int MaxHp, string? Gear,
        List<SpellDto>? Spells, string? Sheet, string? AdventureNotes, bool HasPicture);

    private static bool Safe(string code) => code.Length == 6 && code.All(char.IsAsciiLetterOrDigit);

    public async Task<ImportedFigure?> GetAsync(string figureCode, CancellationToken ct)
    {
        if (!Safe(figureCode)) return null;
        try
        {
            using var r = await http.GetAsync("api/eventyr/" + figureCode, ct);
            if (!r.IsSuccessStatusCode) return null;
            var d = await r.Content.ReadFromJsonAsync<FigureDto>(new JsonSerializerOptions(JsonSerializerDefaults.Web), ct);
            return d is null ? null : new ImportedFigure(d.FigureCode, d.Name ?? "", d.People ?? "", d.Class ?? "", d.Hp, d.MaxHp, d.Gear ?? "",
                (d.Spells ?? []).Select(s => new Spell(s.Name ?? "", s.Text ?? "")).ToList(), d.Sheet ?? "", d.AdventureNotes ?? "", d.HasPicture);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            log.LogWarning("Character import failed: {Error}", ex.Message);
            return null;
        }
    }

    public async Task<byte[]?> PictureAsync(string figureCode, CancellationToken ct)
    {
        if (!Safe(figureCode)) return null;
        try
        {
            using var r = await http.GetAsync($"api/eventyr/{figureCode}/billede", ct);
            return r.IsSuccessStatusCode ? await r.Content.ReadAsByteArrayAsync(ct) : null;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException) { return null; }
    }

    public void SendBack(string figureCode, int hp, int maxHp, string gear, string notes)
    {
        if (!Safe(figureCode)) return;
        _ = Task.Run(async () =>
        {
            try { await http.PostAsJsonAsync("api/eventyr/" + figureCode, new { hp, maxHp, gear, notes }); }
            catch (Exception ex) { log.LogWarning("Character write-back failed: {Error}", ex.Message); }
        });
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

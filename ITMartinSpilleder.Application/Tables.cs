using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Threading.Channels;
using ITMartinSpilleder.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace ITMartinSpilleder.Application;

// The games on disk (one small JSON file per game in Infrastructure).
public interface IGameStore
{
    List<Game> LoadAll();
    void Save(Game game);
    void Delete(string gameId);
}

// Everyone at the table with the page open gets each change at once - but only what they may see (server-sent events).
public interface ILiveFeed
{
    (Guid Id, ChannelReader<string> Reader) Join(string gameId, string viewer);
    void Leave(Guid id);
    /// <param name="to">Which viewers get it; null = everyone in the game.</param>
    void Send(string gameId, string kind, object data, Func<string, bool>? to = null);
}

// Push to the phones (WebPush in Infrastructure), per game and viewer.
public interface IPushSender
{
    string PublicKey { get; }
    void Subscribe(string gameId, string viewer, string endpoint, string p256dh, string auth);
    void Unsubscribe(string endpoint);
    void Send(string gameId, Func<string, bool> to, string title, string body);
}

// Look up what a spell does in the free D&D 5e SRD (CC-BY-4.0, Wizards of the Coast) - Infrastructure calls dnd5eapi.co.
public interface ISpellBook
{
    Task<IReadOnlyList<SpellHit>> SearchAsync(string text, CancellationToken ct);
    Task<Spell?> GetAsync(string index, CancellationToken ct);
}

public sealed record SpellHit(string Index, string Name);

// Is this ITMartin kundenummer a customer who may use Game Master? (Kunder over HTTP in Infrastructure.)
public interface ICustomerCheck
{
    /// <returns>Ok + the customer's name, or an error for the person (English).</returns>
    Task<(bool Ok, string Name, string Error)> CheckAsync(string customerNo, CancellationToken ct);
}

// The figures made in Character (character.itmartin.dk, over HTTP in Infrastructure).
public interface ICharacterSource
{
    /// <returns>The ready figure, or null when the code is not a ready figure (or Character does not answer).</returns>
    Task<ImportedFigure?> GetAsync(string figureCode, CancellationToken ct);
    Task<byte[]?> PictureAsync(string figureCode, CancellationToken ct);
    /// <summary>Hit points, gear and notes back to the figure after a change at the table (fire and forget).</summary>
    void SendBack(string figureCode, int hp, int maxHp, string gear, string notes);
}

// The character pictures (one JPEG per player, files in Infrastructure).
public interface IPictureStore
{
    void Save(string gameId, string playerId, byte[] jpeg);
    byte[]? Load(string gameId, string playerId);
    void Delete(string gameId, string? playerId = null);
}

// What a player's phone gets: the card without the login hash.
public sealed record PlayerView(string Id, string Name, string Character, string Race, string Role, int Hp, int MaxHp, string Notes,
    List<Spell> Spells, int Picture, bool Locked, string Gear, string? Code, string FigureCode, string Sheet)
{
    /// <param name="viewer">The personal code is only shown to that player and to the game master.</param>
    public static PlayerView Of(Player p, string? viewer = null) =>
        new(p.Id, p.Name, p.Character, p.Race, p.Role, p.Hp, p.MaxHp, p.Notes, p.Spells.ToList(), p.Picture, p.Locked, p.Gear,
            viewer == Viewer.Leader || viewer == p.Id ? p.Code : null, p.FigureCode, p.Sheet);
}

public sealed record GameView(string Title, string LeaderName, string JoinCode, string? LeaderCode, string Me, bool Leader,
    List<PlayerView> Players, List<Note> Notes);

public sealed record Joined(string GameId, string Viewer, string Token);

public enum JoinError { None, WrongCode, Locked, Full, NoName, NotInGame, AlreadyInGame, Customer }

public sealed record JoinResult(Joined? Joined, JoinError Error, string Message = "");

// The tables (games). A few friends around a table, so everything is held in memory and each game is written to its own
// file on every change. Every change goes live to the open pages - a secret message only to the ones who may see it.
public sealed class Tables
{
    private readonly IGameStore _store;
    private readonly ILiveFeed _live;
    private readonly IPushSender _push;
    private readonly IPictureStore _pictures;
    private readonly ICustomerCheck _customers;
    private readonly ICharacterSource _characters;
    private readonly TimeProvider _clock;
    private readonly string _createCode;
    private readonly object _gate = new();
    private readonly Dictionary<string, Game> _games;
    private readonly ConcurrentDictionary<string, (int Count, DateTime First)> _wrong = new();

    public const int MinCreateCode = 6, MaxWrongTries = 10;
    public static readonly TimeSpan WrongTriesWindow = TimeSpan.FromMinutes(15);

    public Tables(IGameStore store, ILiveFeed live, IPushSender push, IPictureStore pictures, ICustomerCheck customers, TimeProvider clock,
        string createCode, ICharacterSource characters)
    {
        (_store, _live, _push, _pictures, _customers, _clock, _createCode) = (store, live, push, pictures, customers, clock, createCode);
        _characters = characters;
        var now = Now;
        _games = new();
        foreach (var g in store.LoadAll())
        {
            if (now - g.LastActive > GameRules.KeepIdle) { store.Delete(g.Id); pictures.Delete(g.Id); }
            else _games[g.Id] = g;
        }
    }

    private DateTime Now => _clock.GetUtcNow().UtcDateTime;

    // ---- getting in ----

    /// <summary>Only someone with the app's create code (Spilleder:Code) can start a new game.</summary>
    public (Joined? Joined, JoinError Error) Create(string? createCode, string? title, string? leaderName, string clientIp)
    {
        if (IsLocked(clientIp)) return (null, JoinError.Locked);
        if (_createCode.Length < MinCreateCode || (createCode ?? "").Trim() != _createCode) { Wrong(clientIp); return (null, JoinError.WrongCode); }
        if (GameRules.Cut(leaderName, GameRules.MaxName).Length == 0) return (null, JoinError.NoName);
        var token = GameRules.NewToken();
        var g = GameRules.NewGame(title, leaderName, Now, token);
        lock (_gate)
        {
            while (_games.Values.Any(x => x.JoinCode == g.JoinCode || x.LeaderCode == g.LeaderCode))
                (g.JoinCode, g.LeaderCode) = (GameRules.NewCode(6), GameRules.NewCode(8));
            _games[g.Id] = g;
            _store.Save(g);
        }
        return (new Joined(g.Id, Viewer.Leader, token), JoinError.None);
    }

    // ---- customer numbers (user 2026-10-27: "customer code!", "so dungeonmaster can add players via customer number") ----

    /// <summary>A customer starts a new game and is its game master. Name = the name given, else the name in Kunder.</summary>
    public async Task<JoinResult> StartAsync(string? customerNo, string? title, string? leaderName, string clientIp, CancellationToken ct)
    {
        if (IsLocked(clientIp)) return new(null, JoinError.Locked);
        var k = GameRules.CleanCustomerNo(customerNo);
        if (k.Length == 0) { Wrong(clientIp); return new(null, JoinError.WrongCode); }
        var (ok, name, error) = await _customers.CheckAsync(k, ct);
        if (!ok) { Wrong(clientIp); return new(null, JoinError.Customer, error); }
        var token = GameRules.NewToken();
        var g = GameRules.NewGame(title, GameRules.Cut(leaderName, GameRules.MaxName) is { Length: > 0 } n ? n : name, Now, token, k);
        lock (_gate)
        {
            while (_games.Values.Any(x => x.JoinCode == g.JoinCode || x.LeaderCode == g.LeaderCode))
                (g.JoinCode, g.LeaderCode) = (GameRules.NewCode(6), GameRules.NewCode(8));
            _games[g.Id] = g;
            _store.Save(g);
        }
        _wrong.TryRemove(clientIp, out _);
        return new(new Joined(g.Id, Viewer.Leader, token), JoinError.None);
    }

    /// <summary>
    /// Log in with a kundenummer: the newest game this customer runs as game master, else the newest game they play in.
    /// </summary>
    public async Task<JoinResult> LoginCustomerAsync(string? customerNo, string clientIp, CancellationToken ct)
    {
        if (IsLocked(clientIp)) return new(null, JoinError.Locked);
        var k = GameRules.CleanCustomerNo(customerNo);
        if (k.Length == 0) { Wrong(clientIp); return new(null, JoinError.WrongCode); }
        var (ok, _, error) = await _customers.CheckAsync(k, ct);
        if (!ok) { Wrong(clientIp); return new(null, JoinError.Customer, error); }
        var token = GameRules.NewToken();
        lock (_gate)
        {
            _wrong.TryRemove(clientIp, out _);
            if (_games.Values.Where(x => x.LeaderCustomerNo == k).MaxBy(x => x.LastActive) is { } led)
            {
                led.LeaderTokenHashes.Add(GameRules.Hash(token));
                Touch(led);
                return new(new Joined(led.Id, Viewer.Leader, token), JoinError.None);
            }
            var mine = _games.Values.SelectMany(x => x.Players.Where(p => p.CustomerNo == k).Select(p => (Game: x, Player: p)))
                .OrderByDescending(x => x.Game.LastActive).FirstOrDefault();
            if (mine.Player is null) return new(null, JoinError.NotInGame);
            var i = mine.Game.Players.IndexOf(mine.Player);
            mine.Game.Players[i] = mine.Player with { MoreTokenHashes = [.. (mine.Player.MoreTokenHashes ?? []).TakeLast(9), GameRules.Hash(token)] };
            Touch(mine.Game);
            return new(new Joined(mine.Game.Id, mine.Player.Id, token), JoinError.None);
        }
    }

    /// <summary>The game master adds a customer as a player; they log in with their own kundenummer and are in the game.</summary>
    public async Task<(PlayerView? Player, JoinError Error, string Message)> AddCustomerAsync(string gameId, string viewer, string? customerNo,
        CancellationToken ct)
    {
        if (viewer != Viewer.Leader) return (null, JoinError.WrongCode, "");
        var k = GameRules.CleanCustomerNo(customerNo);
        if (k.Length == 0) return (null, JoinError.WrongCode, "");
        var (ok, name, error) = await _customers.CheckAsync(k, ct);
        if (!ok) return (null, JoinError.Customer, error);
        Game? g;
        Player? p;
        lock (_gate)
        {
            if (!_games.TryGetValue(gameId, out g)) return (null, JoinError.WrongCode, "");
            if (g.Players.Any(x => x.CustomerNo == k)) return (null, JoinError.AlreadyInGame, "");
            p = GameRules.NewPlayer(g, name.Length > 0 ? name : "Player", GameRules.NewToken(), Now, k);
            if (p is null) return (null, JoinError.Full, "");
            g.Players.Add(p);
            Touch(g);
        }
        PlayersChanged(g);
        return (PlayerView.Of(p, viewer), JoinError.None, "");
    }

    /// <summary>The game code makes a new player; the leader code lets the leader in on one more phone.</summary>
    public (Joined? Joined, JoinError Error) Join(string? code, string? name, string clientIp)
    {
        if (IsLocked(clientIp)) return (null, JoinError.Locked);
        var c = GameRules.CleanCode(code);
        var token = GameRules.NewToken();
        Player? p;
        Game? g;
        lock (_gate)
        {
            g = _games.Values.FirstOrDefault(x => x.LeaderCode == c && c.Length == 8);
            if (g is not null)
            {
                g.LeaderTokenHashes.Add(GameRules.Hash(token));
                Touch(g);
                _wrong.TryRemove(clientIp, out _);
                return (new Joined(g.Id, Viewer.Leader, token), JoinError.None);
            }
            // A player's own code: the same card again on this phone.
            var own = c.Length == 8 ? _games.Values.SelectMany(x => x.Players.Select(p => (Game: x, Player: p))).FirstOrDefault(x => x.Player.Code == c) : default;
            if (own.Player is not null)
            {
                var i = own.Game.Players.IndexOf(own.Player);
                own.Game.Players[i] = own.Player with { MoreTokenHashes = [.. (own.Player.MoreTokenHashes ?? []).TakeLast(9), GameRules.Hash(token)] };
                Touch(own.Game);
                _wrong.TryRemove(clientIp, out _);
                return (new Joined(own.Game.Id, own.Player.Id, token), JoinError.None);
            }
            g = _games.Values.FirstOrDefault(x => x.JoinCode == c && c.Length == 6);
            if (g is null) { Wrong(clientIp); return (null, JoinError.WrongCode); }
            if (GameRules.Cut(name, GameRules.MaxName).Length == 0) return (null, JoinError.NoName);
            p = GameRules.NewPlayer(g, name, token, Now);
            if (p is null) return (null, JoinError.Full);
            g.Players.Add(p);
            Touch(g);
        }
        _wrong.TryRemove(clientIp, out _);
        PlayersChanged(g);
        _push.Send(g.Id, v => v == Viewer.Leader, "New player", p.Name + " has joined");
        return (new Joined(g.Id, p.Id, token), JoinError.None);
    }

    /// <summary>The viewer behind a login cookie, or null.</summary>
    public string? WhoIs(string? gameId, string? token)
    {
        lock (_gate) return gameId is not null && _games.TryGetValue(gameId, out var g) ? GameRules.WhoIs(g, token) : null;
    }

    private bool IsLocked(string ip) =>
        _wrong.TryGetValue(ip, out var w) && w.Count >= MaxWrongTries && Now - w.First < WrongTriesWindow;

    private void Wrong(string ip)
    {
        var now = Now;
        _wrong.AddOrUpdate(ip, _ => (1, now), (_, old) => now - old.First > WrongTriesWindow ? (1, now) : (old.Count + 1, old.First));
    }

    // ---- the table ----

    public GameView? View(string gameId, string viewer)
    {
        lock (_gate)
        {
            if (!_games.TryGetValue(gameId, out var g)) return null;
            var leader = viewer == Viewer.Leader;
            return new GameView(g.Title, g.LeaderName, g.JoinCode, leader ? g.LeaderCode : null, viewer, leader,
                g.Players.Select(p => PlayerView.Of(p, viewer)).ToList(), g.Notes.Where(n => GameRules.CanSee(n, viewer)).TakeLast(300).ToList());
        }
    }

    /// <summary>A player edits their own sheet while it is unlocked, and saving locks it; the leader edits any sheet.</summary>
    public PlayerView? EditCard(string gameId, string viewer, string playerId, string? name, string? character, string? race,
        string? role, int hp, int maxHp, string? notes, string? gear, IEnumerable<Spell>? spells) =>
        SendBack(ChangePlayer(gameId, viewer, playerId, p => !GameRules.MayEditSheet(p, viewer) ? null
            : GameRules.EditCard(p, name, character, race, role, hp, maxHp, notes, gear, spells) with { Locked = viewer != Viewer.Leader || p.Locked }));

    /// <summary>
    /// Import a ready figure from Character by its figure code: the card is filled and locked like a saved card
    /// (user 2026-10-27: "data from that should be used during adventure").
    /// </summary>
    public async Task<(PlayerView? Player, string Error)> ImportAsync(string gameId, string viewer, string playerId, string? figureCode,
        CancellationToken ct)
    {
        lock (_gate)
            if (!_games.TryGetValue(gameId, out var g) || g.Players.FirstOrDefault(p => p.Id == playerId) is not { } pl || !GameRules.MayEditSheet(pl, viewer))
                return (null, "You can't change this card now. Ask the game master to unlock it.");
        var f = await _characters.GetAsync(GameRules.CleanCode(figureCode), ct);
        if (f is null) return (null, "We can't find a ready character with that code. In Character, press Ready for adventure first – then you get a 6-letter code.");
        var picture = f.HasPicture ? await _characters.PictureAsync(f.FigureCode, ct) : null;
        if (picture is not null && GameRules.IsPicture(picture)) _pictures.Save(gameId, playerId, picture);
        else picture = null;
        var v = ChangePlayer(gameId, viewer, playerId, p => GameRules.Import(p, f) with
        {
            Picture = picture is null ? p.Picture : p.Picture + 1,
            Locked = viewer != Viewer.Leader || p.Locked,
        });
        return v is null ? (null, "The card could not be saved. Try again.") : (v, "");
    }

    // A card that came from Character sends hit points, gear and notes back there, so the figure carries on to the next adventure.
    private PlayerView? SendBack(PlayerView? p)
    {
        if (p is { FigureCode.Length: > 0 }) _characters.SendBack(p.FigureCode, p.Hp, p.MaxHp, p.Gear, p.Notes);
        return p;
    }

    /// <summary>The leader opens a sheet for changes (fx new weapon) or locks it again.</summary>
    public PlayerView? SetLocked(string gameId, string viewer, string playerId, bool locked)
    {
        if (viewer != Viewer.Leader) return null;
        var p = ChangePlayer(gameId, viewer, playerId, x => x with { Locked = locked });
        if (p is not null && !locked) _push.Send(gameId, v => v == playerId, "You may edit your card", "The game master has unlocked your card.");
        return p;
    }

    /// <summary>The − / + buttons. Everyone sees it at once (and the change, so the screen can flash "−3").</summary>
    public PlayerView? ChangeHp(string gameId, string viewer, string playerId, int delta)
    {
        var p = SendBack(ChangePlayer(gameId, viewer, playerId, x => GameRules.ChangeHp(x, delta)));
        if (p is not null) _live.Send(gameId, "hp", new { id = p.Id, hp = p.Hp, maxHp = p.MaxHp, delta });
        return p;
    }

    public bool RemovePlayer(string gameId, string viewer, string playerId)
    {
        Game? g;
        lock (_gate)
        {
            if (viewer != Viewer.Leader || !_games.TryGetValue(gameId, out g)) return false;
            if (g.Players.RemoveAll(p => p.Id == playerId) == 0) return false;
            _pictures.Delete(gameId, playerId);
            Touch(g);
        }
        PlayersChanged(g);
        return true;
    }

    /// <summary>The character's picture (a player their own, the leader any). Null when not allowed or not a JPEG.</summary>
    public PlayerView? SetPicture(string gameId, string viewer, string playerId, byte[] jpeg)
    {
        if (!GameRules.IsPicture(jpeg)) return null;
        lock (_gate)
        {
            if (!_games.TryGetValue(gameId, out var g) || g.Players.FirstOrDefault(p => p.Id == playerId) is not { } pl
                || !GameRules.MayEditSheet(pl, viewer))
                return null;
            _pictures.Save(gameId, playerId, jpeg);
        }
        return ChangePlayer(gameId, viewer, playerId, p => p with { Picture = p.Picture + 1 });
    }

    /// <summary>Everyone in the game may see every picture.</summary>
    public byte[]? Picture(string gameId, string playerId) => _pictures.Load(gameId, playerId);

    public bool Rename(string gameId, string viewer, string? title, string? leaderName)
    {
        Game? g;
        lock (_gate)
        {
            if (viewer != Viewer.Leader || !_games.TryGetValue(gameId, out g)) return false;
            if (GameRules.Cut(title, GameRules.MaxTitle) is { Length: > 0 } t) g.Title = t;
            if (GameRules.Cut(leaderName, GameRules.MaxName) is { Length: > 0 } n) g.LeaderName = n;
            Touch(g);
        }
        _live.Send(gameId, "spil", new { title = g.Title, leaderName = g.LeaderName });
        return true;
    }

    private PlayerView? ChangePlayer(string gameId, string viewer, string playerId, Func<Player, Player?> f)
    {
        Game? g;
        lock (_gate)
        {
            if (!_games.TryGetValue(gameId, out g)) return null;
            if (viewer != Viewer.Leader && viewer != playerId) return null;
            var i = g.Players.FindIndex(p => p.Id == playerId);
            if (i < 0) return null;
            if (f(g.Players[i]) is not { } changed) return null;
            g.Players[i] = changed;
            Touch(g);
        }
        PlayersChanged(g);
        lock (_gate) return g.Players.Where(p => p.Id == playerId).Select(p => PlayerView.Of(p)).FirstOrDefault();
    }

    private void PlayersChanged(Game g)
    {
        List<PlayerView> list;
        lock (_gate) list = g.Players.Select(p => PlayerView.Of(p)).ToList();
        _live.Send(g.Id, "spillere", list);
    }

    // ---- secret messages and dice ----

    public Note? Write(string gameId, string viewer, IEnumerable<string>? to, string? text) => AddNote(gameId, viewer, to, text, "besked");

    /// <summary>The server rolls, so nobody can cheat. Secret = only the leader sees the roll (the leader's own hidden roll too).</summary>
    public Note? Roll(string gameId, string viewer, string? dice, bool secret, string? reason)
    {
        var r = GameRules.Roll(dice, n => RandomNumberGenerator.GetInt32(n));
        if (r is null) return null;
        var (rolls, bonus, total) = r.Value;
        var why = GameRules.Cut(reason, 60);
        var text = $"rolled {GameRules.Cut(dice, 12).ToLowerInvariant()}{(why.Length > 0 ? " (" + why + ")" : "")}: " +
                   (rolls.Length > 1 || bonus != 0 ? string.Join(" + ", rolls) + (bonus != 0 ? (bonus > 0 ? " + " : " − ") + Math.Abs(bonus) : "") + " = " : "") +
                   total;
        return AddNote(gameId, viewer, secret ? [Viewer.Leader] : [], text, "terning");
    }

    private Note? AddNote(string gameId, string viewer, IEnumerable<string>? to, string? text, string kind)
    {
        Note? n;
        lock (_gate)
        {
            if (!_games.TryGetValue(gameId, out var g)) return null;
            n = GameRules.NewNote(g, viewer, to, text, kind, Now);
            if (n is null) return null;
            g.Notes.Add(n);
            GameRules.Trim(g);
            Touch(g);
        }
        var note = n;
        _live.Send(gameId, "besked", note, v => GameRules.CanSee(note, v));
        _push.Send(gameId, v => v != viewer && GameRules.CanSee(note, v),
            note.To.Count > 0 && viewer == Viewer.Leader ? "Secret message" : note.FromName, (kind == "terning" ? note.FromName + " " : "") + note.Text);
        return note;
    }

    private void Touch(Game g)
    {
        g.LastActive = Now;
        _store.Save(g);
    }
}

public static class DependencyInjection
{
    /// <summary>The use cases. Ports come from AddSpillederInfrastructure.</summary>
    public static IServiceCollection AddSpillederApplication(this IServiceCollection services, string createCode)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(sp => new Tables(sp.GetRequiredService<IGameStore>(), sp.GetRequiredService<ILiveFeed>(),
            sp.GetRequiredService<IPushSender>(), sp.GetRequiredService<IPictureStore>(), sp.GetRequiredService<ICustomerCheck>(),
            sp.GetRequiredService<TimeProvider>(), createCode, sp.GetRequiredService<ICharacterSource>()));
        return services;
    }
}

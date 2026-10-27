using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Threading.Channels;
using ITMartinCharacter.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace ITMartinCharacter.Application;

// The figures on disk (one small JSON file per figure in Infrastructure).
public interface IFigureStore
{
    List<Figure> LoadAll();
    void Save(Figure figure);
    void Delete(string figureId);
}

// The character pictures, one JPEG per figure.
public interface IPictureStore
{
    void Save(string figureId, byte[] jpeg);
    byte[]? Load(string figureId);
    void Delete(string figureId);
}

// The player and the game master with the same figure open see each change at once (server-sent events).
public interface ILiveFeed
{
    (Guid Id, ChannelReader<string> Reader) Join(string figureId, string viewer);
    void Leave(Guid id);
    /// <param name="to">Which viewers get it; null = both.</param>
    void Send(string figureId, string kind, object data, Func<string, bool>? to = null);
}

// Look up what a spell does in the free D&D 5e SRD (CC-BY-4.0, Wizards of the Coast) - Infrastructure calls dnd5eapi.co.
public interface ISpellBook
{
    Task<IReadOnlyList<SpellHit>> SearchAsync(string text, CancellationToken ct);
    Task<Spell?> GetAsync(string index, CancellationToken ct);
}

public sealed record SpellHit(string Index, string Name);

// Is this ITMartin kundenummer a customer? (Kunder over HTTP in Infrastructure, same free app as Game Master.)
public interface ICustomerCheck
{
    /// <returns>Ok + the customer's name, or an error for the person (English).</returns>
    Task<(bool Ok, string Name, string Error)> CheckAsync(string customerNo, CancellationToken ct);
}

// What a phone gets. The game master's private notes only go to the game master. Both see both codes: the player has to send the
// game master code on ("Share with your game master") - at a table of friends that trust is fine.
public sealed record FigureView(string Id, string Viewer, string Kind, IReadOnlyList<string> Steps, string Step, string PlayerName,
    string MasterName, string PlayerCode, string MasterCode, string FigureCode, string Name, string People, string Class,
    string Background, int[] Abilities, int[] Final, string AbilityMethod, List<int> Rolls, int? SuggestedHp, int Hp, int MaxHp,
    string Gear, List<Spell> Spells, string Skills, string Look, string Story, string AdventureNotes, string? MasterNotes,
    int Picture, bool Ready, bool Locked, List<Comment> Comments, DateTime UpdatedAt)
{
    public static FigureView Of(Figure f, string viewer)
    {
        var m = viewer == Viewers.Master;
        return new(f.Id, viewer, f.Kind, Domain.Steps.For(f.Kind), f.Step, f.PlayerName, f.MasterName, f.PlayerCode,
            f.MasterCode, f.FigureCode, f.Name, f.People, f.Class, f.Background, f.Abilities.ToArray(), Srd.Final(f),
            f.AbilityMethod, f.Rolls.ToList(), Srd.StartHp(f), f.Hp, f.MaxHp, f.Gear, f.Spells.ToList(), f.Skills, f.Look, f.Story,
            f.AdventureNotes, m ? f.MasterNotes : null, f.Picture, f.Ready, f.Locked, f.Comments.TakeLast(200).ToList(), f.UpdatedAt);
    }
}

// What Game Master imports (server to server, with the shared key).
public sealed record AdventureView(string FigureCode, string Name, string People, string Class, int Hp, int MaxHp, string Gear,
    List<Spell> Spells, string Sheet, string AdventureNotes, string PlayerName, bool HasPicture);

public sealed record MyFigure(string Id, string Name, string PlayerCode, bool Ready, DateTime UpdatedAt);

public enum OpenError { None, WrongCode, Locked, NoName, Customer }

public sealed record Opened(string FigureId, string Viewer, string Code);

// The workshop: all figures in memory, each written to its own file on every change, every change live to the other person.
// User 2026-10-27: "lots of people put effort into creation, needs to be really good" - so nothing waits for a Save button.
public sealed class Workshop
{
    private readonly IFigureStore _store;
    private readonly IPictureStore _pictures;
    private readonly ILiveFeed _live;
    private readonly ICustomerCheck _customers;
    private readonly TimeProvider _clock;
    private readonly object _gate = new();
    private readonly Dictionary<string, Figure> _figures = new();
    private readonly ConcurrentDictionary<string, (int Count, DateTime First)> _wrong = new();

    public const int MaxWrongTries = 10;
    public static readonly TimeSpan WrongTriesWindow = TimeSpan.FromMinutes(15);

    public Workshop(IFigureStore store, IPictureStore pictures, ILiveFeed live, ICustomerCheck customers, TimeProvider clock)
    {
        (_store, _pictures, _live, _customers, _clock) = (store, pictures, live, customers, clock);
        var now = Now;
        foreach (var f in store.LoadAll())
        {
            if (now - f.UpdatedAt > FigureRules.KeepIdle) { store.Delete(f.Id); pictures.Delete(f.Id); }
            else _figures[f.Id] = f;
        }
    }

    private DateTime Now => _clock.GetUtcNow().UtcDateTime;

    // ---- getting in ----

    /// <summary>A new figure. Guests give a name; customers may give their kundenummer to find their figures on any phone.</summary>
    public async Task<(Opened? Opened, OpenError Error, string Message)> CreateAsync(string? playerName, string? kind, string? customerNo,
        string clientIp, CancellationToken ct)
    {
        if (IsLocked(clientIp)) return (null, OpenError.Locked, "");
        var name = FigureRules.Cut(playerName, 30);
        var k = "";
        if (!string.IsNullOrWhiteSpace(customerNo))
        {
            k = FigureRules.CleanCustomerNo(customerNo);
            if (k.Length == 0) return (null, OpenError.Customer, "A customer number has 8 digits – you find it in the mail from ITMartin.");
            var (ok, cname, error) = await _customers.CheckAsync(k, ct);
            if (!ok) { Wrong(clientIp); return (null, OpenError.Customer, error); }
            if (name.Length == 0) name = cname;
        }
        if (name.Length == 0) return (null, OpenError.NoName, "");
        var f = FigureRules.New(name, kind, Now, k);
        lock (_gate)
        {
            while (_figures.Values.Any(x => x.PlayerCode == f.PlayerCode || x.MasterCode == f.MasterCode || x.MasterCode == f.PlayerCode))
                (f.PlayerCode, f.MasterCode) = (FigureRules.NewCode(8), FigureRules.NewCode(8));
            _figures[f.Id] = f;
            _store.Save(f);
        }
        return (new Opened(f.Id, Viewers.Player, f.PlayerCode), OpenError.None, "");
    }

    /// <summary>The player's code or the game master's code opens the figure.</summary>
    public (Opened? Opened, OpenError Error) Open(string? code, string clientIp)
    {
        if (IsLocked(clientIp)) return (null, OpenError.Locked);
        var c = FigureRules.CleanCode(code);
        lock (_gate)
        {
            if (c.Length == 8)
            {
                if (_figures.Values.FirstOrDefault(x => x.PlayerCode == c) is { } p)
                {
                    _wrong.TryRemove(clientIp, out _);
                    return (new Opened(p.Id, Viewers.Player, c), OpenError.None);
                }
                if (_figures.Values.FirstOrDefault(x => x.MasterCode == c) is { } m)
                {
                    _wrong.TryRemove(clientIp, out _);
                    return (new Opened(m.Id, Viewers.Master, c), OpenError.None);
                }
            }
        }
        Wrong(clientIp);
        return (null, OpenError.WrongCode);
    }

    /// <summary>Who is behind a code on a request (no wrong-try counting - the page already holds a code that worked).</summary>
    public (string FigureId, string Viewer)? WhoIs(string? code)
    {
        var c = FigureRules.CleanCode(code);
        if (c.Length != 8) return null;
        lock (_gate)
        {
            if (_figures.Values.FirstOrDefault(x => x.PlayerCode == c) is { } p) return (p.Id, Viewers.Player);
            if (_figures.Values.FirstOrDefault(x => x.MasterCode == c) is { } m) return (m.Id, Viewers.Master);
        }
        return null;
    }

    /// <summary>A customer's figures, newest first - their kundenummer finds them on any phone.</summary>
    public async Task<(List<MyFigure>? Figures, OpenError Error, string Message)> MineAsync(string? customerNo, string clientIp, CancellationToken ct)
    {
        if (IsLocked(clientIp)) return (null, OpenError.Locked, "");
        var k = FigureRules.CleanCustomerNo(customerNo);
        if (k.Length == 0) { Wrong(clientIp); return (null, OpenError.WrongCode, ""); }
        var (ok, _, error) = await _customers.CheckAsync(k, ct);
        if (!ok) { Wrong(clientIp); return (null, OpenError.Customer, error); }
        _wrong.TryRemove(clientIp, out _);
        lock (_gate)
            return (_figures.Values.Where(f => f.PlayerCustomerNo == k).OrderByDescending(f => f.UpdatedAt)
                .Select(f => new MyFigure(f.Id, f.Name, f.PlayerCode, f.Ready, f.UpdatedAt)).ToList(), OpenError.None, "");
    }

    private bool IsLocked(string ip) =>
        _wrong.TryGetValue(ip, out var w) && w.Count >= MaxWrongTries && Now - w.First < WrongTriesWindow;

    private void Wrong(string ip)
    {
        var now = Now;
        _wrong.AddOrUpdate(ip, _ => (1, now), (_, old) => now - old.First > WrongTriesWindow ? (1, now) : (old.Count + 1, old.First));
    }

    // ---- the figure ----

    public FigureView? View(string figureId, string viewer)
    {
        lock (_gate) return _figures.TryGetValue(figureId, out var f) ? FigureView.Of(f, viewer) : null;
    }

    public FigureView? SetText(string id, string viewer, string field, string? value) => Change(id, viewer, f => FigureRules.SetText(f, viewer, field, value));
    public FigureView? SetKind(string id, string viewer, string? kind) => Change(id, viewer, f => FigureRules.SetKind(f, viewer, kind));
    public FigureView? SetStep(string id, string viewer, string? step) => Change(id, viewer, f => FigureRules.SetStep(f, viewer, step));
    public FigureView? SetAbilities(string id, string viewer, string? method, int[]? scores) =>
        Change(id, viewer, f => FigureRules.SetAbilities(f, viewer, method, scores));
    public FigureView? RollAbilities(string id, string viewer) =>
        Change(id, viewer, f => FigureRules.RollAbilities(f, viewer, n => RandomNumberGenerator.GetInt32(n)));
    public FigureView? SetHp(string id, string viewer, int hp, int maxHp) => Change(id, viewer, f => FigureRules.SetHp(f, viewer, hp, maxHp));
    public FigureView? SetSpells(string id, string viewer, IEnumerable<Spell>? spells) => Change(id, viewer, f => FigureRules.SetSpells(f, viewer, spells));
    public FigureView? SetLocked(string id, string viewer, bool locked) => Change(id, viewer, f => FigureRules.SetLocked(f, viewer, locked));

    public FigureView? MakeReady(string id, string viewer)
    {
        lock (_gate)
        {
            var taken = _figures.Values.Select(x => x.FigureCode).Where(c => c.Length > 0).ToHashSet();
            return Change(id, viewer, f => FigureRules.MakeReady(f, viewer, taken.Contains));
        }
    }

    public Comment? AddComment(string id, string viewer, string? step, string? text)
    {
        Comment? c;
        lock (_gate)
        {
            if (!_figures.TryGetValue(id, out var f)) return null;
            c = FigureRules.NewComment(f, viewer, step, text, Now);
            if (c is null) return null;
            Touch(f);
        }
        _live.Send(id, "kommentar", c);
        return c;
    }

    public FigureView? SetPicture(string id, string viewer, byte[] jpeg)
    {
        if (!FigureRules.IsPicture(jpeg)) return null;
        lock (_gate)
        {
            if (!_figures.TryGetValue(id, out var f) || !FigureRules.MayEdit(f, viewer)) return null;
            _pictures.Save(id, jpeg);
        }
        return Change(id, viewer, f => { f.Picture++; return true; });
    }

    public byte[]? Picture(string id) => _pictures.Load(id);

    // Both people see every change; the game master's private notes only go to the game master.
    private FigureView? Change(string id, string viewer, Func<Figure, bool> f)
    {
        Figure? fig;
        lock (_gate)
        {
            if (!_figures.TryGetValue(id, out fig) || !f(fig)) return null;
            Touch(fig);
        }
        Broadcast(fig);
        lock (_gate) return FigureView.Of(fig, viewer);
    }

    private void Broadcast(Figure f)
    {
        FigureView player, master;
        lock (_gate) (player, master) = (FigureView.Of(f, Viewers.Player), FigureView.Of(f, Viewers.Master));
        _live.Send(f.Id, "figur", player, v => v == Viewers.Player);
        _live.Send(f.Id, "figur", master, v => v == Viewers.Master);
    }

    private void Touch(Figure f)
    {
        f.UpdatedAt = Now;
        _store.Save(f);
    }

    // ---- Game Master (server to server) ----

    public AdventureView? ForAdventure(string? figureCode)
    {
        var c = FigureRules.CleanCode(figureCode);
        lock (_gate)
            return _figures.Values.FirstOrDefault(f => f.Ready && f.FigureCode == c && c.Length == 6) is { } f
                ? new AdventureView(f.FigureCode, f.Name, f.People, f.Class, f.Hp, f.MaxHp, f.Gear, f.Spells.ToList(), FigureRules.Sheet(f),
                    f.AdventureNotes, f.PlayerName, f.Picture > 0)
                : null;
    }

    public byte[]? AdventurePicture(string? figureCode) =>
        ForAdventure(figureCode) is { HasPicture: true } a ? FindByFigureCode(a.FigureCode) is { } id ? _pictures.Load(id) : null : null;

    /// <summary>After play: hit points, gear and what happened come back from Game Master.</summary>
    public bool BackFromAdventure(string? figureCode, int hp, int maxHp, string? gear, string? notes)
    {
        Figure? f;
        lock (_gate)
        {
            if (FindByFigureCode(figureCode) is not { } id) return false;
            f = _figures[id];
            FigureRules.BackFromAdventure(f, hp, maxHp, gear, notes);
            Touch(f);
        }
        Broadcast(f);
        return true;
    }

    private string? FindByFigureCode(string? figureCode)
    {
        var c = FigureRules.CleanCode(figureCode);
        lock (_gate) return _figures.Values.FirstOrDefault(f => f.Ready && c.Length == 6 && f.FigureCode == c)?.Id;
    }
}

public static class DependencyInjection
{
    /// <summary>The use cases. Ports come from AddCharacterInfrastructure.</summary>
    public static IServiceCollection AddCharacterApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<Workshop>();
        return services;
    }
}

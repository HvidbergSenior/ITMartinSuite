using ITMartinKigMed.Domain;
using Microsoft.Extensions.Logging;

namespace ITMartinKigMed.Application;

// One live session at a time: Martin in the studio, viewers on the front page. Every page calls this; it holds the
// session under one lock (viewers add from other circuits meanwhile), saves it on every change and tells all open
// pages to redraw via Changed. Rules live in the Domain (LiveSession, Floor, SendLimits).
public sealed class LiveHub
{
    public sealed record Watcher(string Name, DateTime Since);

    private readonly object _lock = new();
    private readonly ISessionStore _store;
    private readonly IPictureStore _pictures;
    private readonly TimeProvider _clock;
    private readonly ILogger<LiveHub> _log;
    private readonly LiveSession _s;
    private readonly Floor _floor = new();
    private readonly SendLimits _limits = new();
    // circuits that have the viewer page open -> who it is (name typed on the page, "" until then) and since when
    private readonly Dictionary<string, (string Name, DateTime Since)> _watching = [];

    public event Action? Changed;
    public event Action<string>? Reacted;   // emoji - drawn as a floating emoji on every open page

    public LiveHub(ISessionStore store, IPictureStore pictures, TimeProvider clock, ILogger<LiveHub> log)
    {
        (_store, _pictures, _clock, _log) = (store, pictures, clock, log);
        _s = store.Load();
    }

    private DateTime Now => _clock.GetUtcNow().UtcDateTime;

    // Pages render from a copy taken under the lock.
    public T Read<T>(Func<LiveSession, T> read) { lock (_lock) return read(_s); }

    // ── who is watching ──
    public int Viewers { get { lock (_lock) return _watching.Count; } }

    public void Watch(string circuit, bool on, string name = "")
    {
        lock (_lock)
        {
            if (!on) _watching.Remove(circuit);
            else _watching[circuit] = (Texts.Clip(name, 40), _watching.TryGetValue(circuit, out var w) ? w.Since : Now);
        }
        Changed?.Invoke();
    }

    // For the studio: who has the page open right now, newest first.
    public List<Watcher> Watchers() { lock (_lock) return _watching.Values.OrderByDescending(w => w.Since).Select(w => new Watcher(w.Name, w.Since)).ToList(); }

    // ── the floor ("Ræk hånden op") ──
    public List<Hand> Hands() { lock (_lock) return [.. _floor.Hands]; }
    public Stage? OnStage { get { lock (_lock) return _floor.OnStage; } }
    public void RaiseHand(string viewer, string name) { lock (_lock) _floor.Raise(viewer, name, Now); Changed?.Invoke(); }
    public void LowerHand(string viewer) { lock (_lock) _floor.Lower(viewer); Changed?.Invoke(); }
    public void GiveFloor(string viewer) { lock (_lock) _floor.Give(viewer, Now); Changed?.Invoke(); }
    public void EndFloor(string? viewer = null) { lock (_lock) _floor.End(viewer); Changed?.Invoke(); }
    public bool GuestTokenOk(string? token) { lock (_lock) return _floor.TokenOk(token); }

    // ── Martin (studio) ──
    public void GoLive(string title) => Change(s => s.GoLive(title, Now));

    // Ends the session and archives it (questions, ideas, polls) to sessions/<start>.json.
    public void EndLive()
    {
        lock (_lock)
        {
            if (!_s.End()) return;
            _floor.Clear();
            try { _store.Archive(_s, _s.ArchiveName(Now)); }
            catch (Exception ex) { _log.LogWarning(ex, "Could not archive the session"); }
        }
        Save();
    }

    public void SetTexts(string title, string next) => Change(s => s.SetTexts(title, next));
    public void SetStatus(Guid id, string status) => Change(s => s.SetStatus(id, status));
    public void DeleteQuestion(Guid id) => Change(s => s.DeleteQuestion(id));
    public void ShowIdea(Guid id, bool shown) => Change(s => s.ShowIdea(id, shown));
    public void DeleteIdea(Guid id) => Change(s => s.DeleteIdea(id));
    public void StartPoll(string text, IEnumerable<string> options) => Change(s => s.StartPoll(text, options));
    public void ClosePoll() => Change(s => s.ClosePoll());

    // Something outside the session changed (e.g. the video started/stopped arriving): redraw the pages.
    public void Touch() => Changed?.Invoke();

    // ── viewers ──
    // false = too fast (one message per 3 s) or empty.
    public bool Ask(string viewer, string name, string text, bool fromMartin = false)
    {
        lock (_lock)
        {
            if (Texts.Clip(text, LiveSession.MaxText).Length == 0) return false;
            if (!fromMartin && !_limits.MayAsk(viewer, Now)) return false;
            _s.Ask(viewer, name, text, Now, fromMartin);
        }
        Save();
        return true;
    }

    public bool Vote(string viewer, Guid poll, int option)
    {
        lock (_lock) if (!_s.Vote(viewer, poll, option)) return false;
        Save();
        return true;
    }

    // Reactions are counted but not saved on every tap (they arrive in bursts); the next change saves them.
    public void React(string viewer, string emoji)
    {
        lock (_lock)
        {
            if (!LiveSession.Emojis.Contains(emoji) || !_limits.MayReact(viewer, Now)) return;
            _s.React(emoji);
        }
        Reacted?.Invoke(emoji);
        Changed?.Invoke();
    }

    public bool SendIdea(string viewer, string name, string text, string? picture)
    {
        lock (_lock) if (!_s.SendIdea(viewer, name, text, picture, Now)) return false;
        Save();
        return true;
    }

    public Task<string> SavePictureAsync(Stream jpeg, CancellationToken ct = default) => _pictures.SaveAsync(jpeg, ct);

    // A sent-in picture: Martin may always see it, everybody else only once he has shown it.
    public string? PictureFile(string name, bool owner)
    {
        var file = _pictures.PathOf(name);
        if (file is null) return null;
        return owner || Read(s => s.PictureIsShown(name)) ? file : null;
    }

    private void Change(Action<LiveSession> change)
    {
        lock (_lock) change(_s);
        Save();
    }

    private void Save()
    {
        try { lock (_lock) _store.Save(_s); }
        catch (Exception ex) { _log.LogWarning(ex, "Could not save the session"); }
        Changed?.Invoke();
    }
}

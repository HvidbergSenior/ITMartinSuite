using System.Text.Json;

namespace ITMartinKigMed.Server.Services;

// One live session at a time: Martin in the studio, viewers on the front page. Everything viewers send
// (questions, votes, reactions, ideas/pictures) lives here and is pushed to every open page via events.
// The session is saved to /app/data/session.json on every change, so a restart does not lose it; an ended
// session is archived to /app/data/sessions/<start>.json.
public sealed class LiveHub
{
    public sealed record Question(Guid Id, string Viewer, string Name, string Text, DateTime At, bool FromMartin = false)
    {
        public string Status { get; set; } = "ny";   // ny | svarer | færdig
    }

    public sealed record Idea(Guid Id, string Viewer, string Name, string Text, string? Picture, DateTime At)
    {
        public bool Shown { get; set; }   // Martin chose "Vis for alle"
    }

    public sealed class Poll
    {
        public Guid Id { get; init; } = Guid.NewGuid();
        public string Text { get; init; } = "";
        public List<string> Options { get; init; } = [];
        public Dictionary<string, int> Votes { get; init; } = [];   // viewer key -> option index
        public bool Open { get; set; } = true;
        public int[] Counts() => Options.Select((_, i) => Votes.Values.Count(v => v == i)).ToArray();
    }

    public sealed class State
    {
        public bool Live { get; set; }
        public string Title { get; set; } = "";
        public string Next { get; set; } = "";   // "Næste gang: torsdag kl. 20" - shown while offline
        public DateTime? StartedAt { get; set; }
        public List<Question> Questions { get; init; } = [];
        public List<Idea> Ideas { get; init; } = [];
        public List<Poll> Polls { get; init; } = [];
        public Dictionary<string, int> Reactions { get; init; } = [];
    }

    public const int MaxQuestions = 500, MaxIdeas = 200, MaxText = 400;
    public static readonly string[] Emojis = ["👍", "❤️", "😂", "🔥", "👏", "🎸", "🤯", "🐦"];

    private readonly object _lock = new();
    private readonly string _dir, _file;
    private readonly ILogger<LiveHub> _log;
    private readonly Dictionary<string, DateTime> _lastSend = [];
    private readonly Dictionary<string, (DateTime Window, int Count)> _reactRate = [];
    private readonly HashSet<string> _watching = [];   // circuits that have the viewer page open

    public State S { get; }

    public event Action? Changed;
    public event Action<string>? Reacted;   // emoji - drawn as a floating emoji on every open page

    public LiveHub(IConfiguration cfg, ILogger<LiveHub> log)
    {
        _log = log;
        _dir = cfg["KigMed:DataDir"] ?? "/app/data";
        Directory.CreateDirectory(Path.Combine(_dir, "sessions"));
        Directory.CreateDirectory(Path.Combine(_dir, "pictures"));
        _file = Path.Combine(_dir, "session.json");
        try { S = File.Exists(_file) ? JsonSerializer.Deserialize<State>(File.ReadAllText(_file)) ?? new() : new(); }
        catch (Exception ex) { _log.LogWarning(ex, "session.json unreadable - starting empty"); S = new(); }
    }

    // Pages render from a copy taken under the lock (viewers add to the lists from other circuits meanwhile).
    public T Read<T>(Func<State, T> read) { lock (_lock) return read(S); }

    public string PicturesDir => Path.Combine(_dir, "pictures");
    public int Viewers { get { lock (_lock) return _watching.Count; } }

    public void Watch(string circuit, bool on)
    {
        lock (_lock) { if (on) _watching.Add(circuit); else _watching.Remove(circuit); }
        Changed?.Invoke();
    }

    // ── Martin (studio) ──
    public void GoLive(string title)
    {
        lock (_lock)
        {
            if (!S.Live) { S.Questions.Clear(); S.Ideas.Clear(); S.Polls.Clear(); S.Reactions.Clear(); S.StartedAt = DateTime.UtcNow; }
            S.Live = true;
            S.Title = Clip(title, 120);
        }
        Save();
    }

    public void EndLive()
    {
        lock (_lock)
        {
            if (!S.Live) return;
            S.Live = false;
            try
            {
                var name = (S.StartedAt ?? DateTime.UtcNow).ToString("yyyy-MM-dd_HHmm") + ".json";
                File.WriteAllText(Path.Combine(_dir, "sessions", name), JsonSerializer.Serialize(S, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception ex) { _log.LogWarning(ex, "Could not archive the session"); }
        }
        Save();
    }

    public void SetTexts(string title, string next)
    {
        lock (_lock) { S.Title = Clip(title, 120); S.Next = Clip(next, 200); }
        Save();
    }

    public void SetStatus(Guid id, string status)
    {
        lock (_lock)
        {
            // Only one question is "svarer nu" at a time.
            if (status == "svarer") foreach (var q in S.Questions.Where(q => q.Status == "svarer")) q.Status = "færdig";
            if (S.Questions.FirstOrDefault(q => q.Id == id) is { } hit) hit.Status = status;
        }
        Save();
    }

    public void DeleteQuestion(Guid id) { lock (_lock) S.Questions.RemoveAll(q => q.Id == id); Save(); }
    public void ShowIdea(Guid id, bool shown) { lock (_lock) if (S.Ideas.FirstOrDefault(i => i.Id == id) is { } hit) hit.Shown = shown; Save(); }
    public void DeleteIdea(Guid id) { lock (_lock) S.Ideas.RemoveAll(i => i.Id == id); Save(); }

    public void StartPoll(string text, IEnumerable<string> options)
    {
        var opts = options.Select(o => Clip(o, 60)).Where(o => o.Length > 0).Take(6).ToList();
        if (opts.Count < 2) return;
        lock (_lock)
        {
            foreach (var p in S.Polls) p.Open = false;
            S.Polls.Add(new Poll { Text = Clip(text, 160), Options = opts });
        }
        Save();
    }

    public void ClosePoll() { lock (_lock) foreach (var p in S.Polls) p.Open = false; Save(); }

    // ── viewers ──
    // false = too fast (one message per 3 s) or empty.
    public bool Ask(string viewer, string name, string text, bool fromMartin = false)
    {
        text = Clip(text, MaxText);
        if (text.Length == 0 || !fromMartin && !Throttle(viewer)) return false;
        lock (_lock)
        {
            S.Questions.Add(new Question(Guid.NewGuid(), viewer, fromMartin ? "Martin" : NameOf(name), text, DateTime.UtcNow, fromMartin));
            if (S.Questions.Count > MaxQuestions) S.Questions.RemoveAt(0);
        }
        Save();
        return true;
    }

    public bool Vote(string viewer, Guid poll, int option)
    {
        lock (_lock)
        {
            if (S.Polls.FirstOrDefault(p => p.Id == poll && p.Open) is not { } p || option < 0 || option >= p.Options.Count) return false;
            p.Votes[viewer] = option;   // changing your mind is allowed, one vote per phone
        }
        Save();
        return true;
    }

    public void React(string viewer, string emoji)
    {
        if (!Emojis.Contains(emoji)) return;
        lock (_lock)
        {
            // at most 8 reactions per 5 s per viewer - enough to be happy, not enough to flood
            var now = DateTime.UtcNow;
            var (window, count) = _reactRate.GetValueOrDefault(viewer);
            if (now - window > TimeSpan.FromSeconds(5)) (window, count) = (now, 0);
            if (count >= 8) return;
            _reactRate[viewer] = (window, count + 1);
            S.Reactions[emoji] = S.Reactions.GetValueOrDefault(emoji) + 1;
        }
        Reacted?.Invoke(emoji);
        Changed?.Invoke();
    }

    public bool SendIdea(string viewer, string name, string text, string? picture)
    {
        text = Clip(text, MaxText);
        if (text.Length == 0 && picture is null) return false;
        lock (_lock)
        {
            if (S.Ideas.Count(i => i.Viewer == viewer) >= 10 || S.Ideas.Count >= MaxIdeas) return false;
            S.Ideas.Add(new Idea(Guid.NewGuid(), viewer, NameOf(name), text, picture, DateTime.UtcNow));
        }
        Save();
        return true;
    }

    private bool Throttle(string viewer)
    {
        lock (_lock)
        {
            if (_lastSend.TryGetValue(viewer, out var last) && DateTime.UtcNow - last < TimeSpan.FromSeconds(3)) return false;
            _lastSend[viewer] = DateTime.UtcNow;
            return true;
        }
    }

    private static string NameOf(string name) => Clip(name, 40) is { Length: > 0 } n ? n : "Gæst";
    private static string Clip(string? s, int max) { s = (s ?? "").Trim(); return s.Length > max ? s[..max] : s; }

    private void Save()
    {
        try { lock (_lock) File.WriteAllText(_file, JsonSerializer.Serialize(S)); }
        catch (Exception ex) { _log.LogWarning(ex, "Could not save session.json"); }
        Changed?.Invoke();
    }
}

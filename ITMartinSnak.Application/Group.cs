using ITMartinSnak.Domain;

namespace ITMartinSnak.Application;

// What a Group knows about its room: where it lives, how a push links back, and who to tell when something happens.
public sealed record RoomContext(string Id, string Title, string Url, Action<bool> Activity);

// One room's chat and tasks. Held in memory and written to disk on each change. Every change goes live to the open
// pages in that room and as a push to the room's other phones (and Martin's).
public sealed class Group
{
    private readonly IGroupStore _store;
    private readonly ILiveFeed _live;
    private readonly IPushSender _push;
    private readonly TimeProvider _clock;
    private readonly RoomContext _room;
    private readonly object _gate = new();
    private readonly List<Message> _messages;
    private readonly List<TaskItem> _tasks;
    private readonly List<SharedFile> _files;
    private readonly List<Seen> _seen;
    private readonly Dictionary<string, List<long>> _mine;
    private readonly List<Profile> _profiles;

    public Group(IGroupStore store, ILiveFeed live, IPushSender push, TimeProvider clock, RoomContext room)
    {
        (_store, _live, _push, _clock, _room) = (store, live, push, clock, room);
        _messages = store.LoadMessages();
        _tasks = store.LoadTasks();
        _files = store.LoadFiles();
        _seen = store.LoadSeen();
        _mine = new(store.LoadMine(), StringComparer.OrdinalIgnoreCase);
        _profiles = store.LoadProfiles();
    }

    // ---- chat ----

    public IReadOnlyList<Message> MessagesAfter(long afterId, int max = 300)
    {
        lock (_gate) return _messages.Where(m => m.Id > afterId).TakeLast(max).ToList();
    }

    public Message? Last { get { lock (_gate) return _messages.Count == 0 ? null : _messages[^1]; } }
    public bool Waiting { get { lock (_gate) return RoomRules.Waiting(_messages); } }

    public Message? Write(string? name, string? text, string? fromEndpoint = null, bool host = false)
    {
        Message? m;
        lock (_gate)
        {
            m = GroupRules.NewMessage(_messages, name, text, _clock.GetUtcNow().UtcDateTime, host);
            if (m is null) return null;
            _messages.Add(m);
            GroupRules.Trim(_messages);
            _store.SaveMessages(_messages);
        }
        _live.Send(_room.Id, "besked", m);
        _push.SendToOthers(_room.Id, fromEndpoint, $"{m.Name} · {_room.Title}", m.Text, "snak-" + _room.Id, _room.Url);
        _room.Activity(true);
        return m;
    }

    // ---- profiles (2026-11-09): what each participant chooses to share - mail, phone, website, LinkedIn, a line about them ----

    public IReadOnlyList<Profile> Profiles()
    {
        lock (_gate) return _profiles.ToList();
    }

    /// <summary>Saves the person's own card; an all-empty card is removed. Everyone in the room sees it at once.</summary>
    public (Profile? Profile, string? Error) SaveProfile(string? name, string? mail, string? phone, string? web, string? linkedIn, string? about)
    {
        var (p, err) = ProfileRules.Clean(name, mail, phone, web, linkedIn, about);
        if (p is null) return (null, err);
        lock (_gate)
        {
            _profiles.RemoveAll(x => string.Equals(x.Name, p.Name, StringComparison.OrdinalIgnoreCase));
            if (!ProfileRules.IsEmpty(p)) _profiles.Add(p);
            _store.SaveProfiles(_profiles);
        }
        _live.Send(_room.Id, "profiler", Profiles());
        return (p, null);
    }

    // ---- "Til næste gang" (2026-11-09, course groups): Martin adds the assignment, each person ticks it for themselves.
    // The tick is only theirs - nobody else sees it (user: "each ticks own", no pressure).

    public IReadOnlyList<long> MyDone(string? name)
    {
        var n = GroupRules.CleanName(name);
        lock (_gate) return n.Length > 0 && _mine.TryGetValue(n, out var ids) ? ids.ToList() : [];
    }

    public IReadOnlyList<long>? SetMyDone(string? name, long taskId, bool done)
    {
        var n = GroupRules.CleanName(name);
        if (n.Length == 0) return null;
        lock (_gate)
        {
            if (!_tasks.Any(t => t.Id == taskId)) return null;
            var ids = _mine.TryGetValue(n, out var l) ? l : _mine[n] = [];
            ids.Remove(taskId);
            if (done) ids.Add(taskId);
            _store.SaveMine(_mine);
            return ids.ToList();
        }
    }

    // ---- seen (2026-11-09): how far each name has read, and how their phone is set up ----

    /// <summary>Everyone in the room gets the read positions (names + message ids only, never the phone details).</summary>
    public IReadOnlyList<object> ReadPositions()
    {
        lock (_gate) return _seen.Select(s => (object)new { navn = s.Name, besked = s.MessageId }).ToList();
    }

    public IReadOnlyList<Seen> SeenList()
    {
        lock (_gate) return _seen.ToList();
    }

    public Seen? MarkSeen(string? name, long messageId, string? device, bool homeScreen, string? push, bool inApp)
    {
        Seen merged;
        bool moved;
        lock (_gate)
        {
            var last = _messages.Count == 0 ? 0 : _messages[^1].Id;
            var fresh = SeenRules.Clean(name, Math.Min(messageId, last), _clock.GetUtcNow().UtcDateTime, device, homeScreen, push, inApp);
            if (fresh is null) return null;
            var i = _seen.FindIndex(s => string.Equals(s.Name, fresh.Name, StringComparison.OrdinalIgnoreCase));
            var old = i < 0 ? null : _seen[i];
            merged = SeenRules.Merge(old, fresh);
            if (!SeenRules.WorthSaving(old, merged)) return old;
            if (i < 0) _seen.Add(merged); else _seen[i] = merged;
            _store.SaveSeen(_seen);
            moved = old is null || old.MessageId != merged.MessageId;
        }
        if (moved) _live.Send(_room.Id, "set", ReadPositions());
        return merged;
    }

    // ---- tasks (the host makes them, everyone can take one) ----

    public IReadOnlyList<TaskItem> Tasks()
    {
        lock (_gate) return _tasks.Select(GroupRules.Copy).ToList();
    }

    // Danish time decides what "today" is (the server runs in UTC).
    private static readonly TimeZoneInfo Dk = TimeZoneInfo.FindSystemTimeZoneById("Europe/Copenhagen");
    public DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(_clock.GetUtcNow().UtcDateTime, Dk));

    /// <param name="by">A member who adds it themselves (shared rooms, 2026-11-12) - "" = the host.</param>
    public TaskItem? AddTask(string? title, string? note, string? when, string? fromEndpoint = null, string? to = null, string? day = null, bool daily = false,
        string? by = null, bool yearly = false)
    {
        TaskItem? t;
        lock (_gate)
        {
            t = GroupRules.NewTask(_tasks, title, note, when, _clock.GetUtcNow().UtcDateTime, to, day, daily, by, yearly);
            if (t is null) return null;
            _tasks.Add(t);
            _store.SaveTasks(_tasks);
        }
        TasksChanged();
        var head = t.By.Length > 0 ? $"{t.By} har lagt noget ind · " : t.Takers.Count > 0 ? $"Ny opgave til {t.Takers[0]} · " : "Ny opgave · ";
        _push.SendToOthers(_room.Id, fromEndpoint, head + _room.Title, t.Title + (t.When.Length > 0 ? " · " + t.When : ""), "snak-opg-" + _room.Id, _room.Url);
        _room.Activity(false);
        return GroupRules.Copy(t);
    }

    /// <param name="to">null = keep who has it; "" = nobody; a name = give it to that person (they get a push).</param>
    public TaskItem? EditTask(long id, string? title, string? note, string? when, string? to = null, string? fromEndpoint = null, string? day = null, bool? daily = null,
        bool? yearly = null)
    {
        string? before;
        lock (_gate) before = _tasks.FirstOrDefault(x => x.Id == id) is { } old ? string.Join(",", old.Takers) : null;
        var t = Change(id, x => GroupRules.Edit(x, title, note, when, to, day, daily, yearly));
        if (t is not null && to is not null && t.Takers.Count > 0 && string.Join(",", t.Takers) != before)
            _push.SendToOthers(_room.Id, fromEndpoint, $"Opgave til {t.Takers[0]} · " + _room.Title, t.Title + (t.When.Length > 0 ? " · " + t.When : ""), "snak-opg-" + _room.Id, _room.Url);
        return t;
    }

    public TaskItem? SetDone(long id, bool done) => Change(id, t => GroupRules.SetDone(t, done, Today));

    public TaskItem? ToggleTaker(long id, string? name, string? fromEndpoint = null)
    {
        var t = Change(id, x => GroupRules.ToggleTaker(x, name));
        if (t is not null && GroupRules.HasTaker(t, name))
            _push.SendToOthers(_room.Id, fromEndpoint, "Opgave taget · " + _room.Title, $"{GroupRules.CleanName(name)} tager: {t.Title}", "snak-opg-" + _room.Id, _room.Url);
        return t;
    }

    public bool DeleteTask(long id)
    {
        lock (_gate)
        {
            if (_tasks.RemoveAll(t => t.Id == id) == 0) return false;
            _store.SaveTasks(_tasks);
        }
        TasksChanged();
        return true;
    }

    /// <summary>May this name (or the host) change or remove the task? Members only what they added themselves.</summary>
    public bool MayChangeTask(long id, string? name, bool host)
    {
        lock (_gate) return _tasks.FirstOrDefault(t => t.Id == id) is { } t && GroupRules.MayChange(t, name, host);
    }

    private TaskItem? Change(long id, Func<TaskItem, TaskItem?> f)
    {
        TaskItem? changed;
        lock (_gate)
        {
            var i = _tasks.FindIndex(t => t.Id == id);
            if (i < 0) return null;
            changed = f(_tasks[i]);
            if (changed is null) return null;
            _tasks[i] = changed;
            _store.SaveTasks(_tasks);
        }
        TasksChanged();
        return GroupRules.Copy(changed);
    }

    private void TasksChanged() => _live.Send(_room.Id, "opgaver", Tasks());

    // ---- documents (everyone adds and downloads; the one who added it, or Martin, removes it) ----

    public IReadOnlyList<SharedFile> Files()
    {
        lock (_gate) return _files.OrderByDescending(f => f.Id).ToList();
    }

    public SharedFile? File(long id)
    {
        lock (_gate) return _files.FirstOrDefault(f => f.Id == id);
    }

    public Stream? OpenFile(SharedFile f) => _store.OpenContent(f.StoredName);

    /// <returns>The new file, or the Danish reason it was not added.</returns>
    /// <param name="topic">Already checked against the room's topics (RosterRules.TopicFor); "" = Andet.</param>
    public async Task<(SharedFile? File, string? Error)> AddFileAsync(string? fileName, long size, Stream content, string? by,
        string? fromEndpoint, CancellationToken ct = default, string topic = "")
    {
        var who = GroupRules.CleanName(by);
        if (who.Length == 0) return (null, "Skriv dit navn først.");
        var name = FileRules.CleanName(fileName);
        SharedFile f;
        lock (_gate)
        {
            if (FileRules.Check(name, size, _files.Count) is { } err) return (null, err);
            f = FileRules.New(_files, name, size, who, _clock.GetUtcNow().UtcDateTime, topic);
            _files.Add(f);   // holds the id while the bytes are written
        }
        var ok = false;
        try { ok = await _store.WriteContentAsync(f.StoredName, content, FileRules.MaxSize, ct); }
        finally
        {
            lock (_gate)
            {
                if (ok) _store.SaveFiles(_files); else _files.Remove(f);
            }
        }
        if (!ok) return (null, "Filen er tom eller for stor. Den må højst være 25 MB.");
        FilesChanged();
        _push.SendToOthers(_room.Id, fromEndpoint, "Ny fil · " + _room.Title, $"{who} har lagt {f.Name} op", "snak-fil-" + _room.Id, _room.Url);
        _room.Activity(false);
        return (f, null);
    }

    public bool DeleteFile(long id, string? name, bool host)
    {
        SharedFile? f;
        lock (_gate)
        {
            f = _files.FirstOrDefault(x => x.Id == id);
            if (f is null || !FileRules.MayDelete(f, name, host)) return false;
            _files.Remove(f);
            _store.SaveFiles(_files);
        }
        _store.DeleteContent(f.StoredName);
        FilesChanged();
        return true;
    }

    private void FilesChanged() => _live.Send(_room.Id, "filer", Files());

    // Reminders already sent: "taskId|date|minutes". In memory - after a restart at most one repeat inside the 10-minute window.
    private readonly HashSet<string> _reminded = [];

    /// <summary>Pushes the reminders that are due now to every phone in the room. Returns how many went out.</summary>
    public int SendReminders()
    {
        var now = TimeZoneInfo.ConvertTimeFromUtc(_clock.GetUtcNow().UtcDateTime, Dk);
        var due = new List<(TaskItem T, int M)>();
        lock (_gate)
        {
            foreach (var t in _tasks)
                foreach (var m in GroupRules.DueReminders(t, now))
                    if (_reminded.Add($"{t.Id}|{now:yyyy-MM-dd}|{m}")) due.Add((GroupRules.Copy(t), m));
            if (_reminded.Count > 5000) _reminded.Clear();
        }
        foreach (var (t, m) in due)
            _push.SendToOthers(_room.Id, null, "Påmindelse · " + _room.Title, GroupRules.ReminderText(t, m), $"snak-pam-{_room.Id}-{t.Id}", _room.Url);
        return due.Count;
    }
}

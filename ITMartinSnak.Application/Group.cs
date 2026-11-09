using System.Collections.Concurrent;
using System.Threading.Channels;
using ITMartinSnak.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace ITMartinSnak.Application;

// One room's messages and tasks on disk (two small JSON files per room in Infrastructure).
public interface IGroupStore
{
    List<Message> LoadMessages();
    void SaveMessages(IReadOnlyList<Message> messages);
    List<TaskItem> LoadTasks();
    void SaveTasks(IReadOnlyList<TaskItem> tasks);
    List<SharedFile> LoadFiles();
    void SaveFiles(IReadOnlyList<SharedFile> files);
    /// <summary>Writes the content; false (and nothing kept) when it is empty or bigger than <paramref name="maxBytes"/>.</summary>
    Task<bool> WriteContentAsync(string storedName, Stream content, long maxBytes, CancellationToken ct);
    Stream? OpenContent(string storedName);
    void DeleteContent(string storedName);
}

// The list of rooms, and a store per room.
public interface IRoomStore
{
    List<Room> LoadRooms();
    void SaveRooms(IReadOnlyList<Room> rooms);
    IGroupStore For(string roomId);
    void DeleteRoomData(string roomId);
}

// Everyone with a room open gets each change in that room at once (server-sent events in Infrastructure).
public interface ILiveFeed
{
    void Send(string room, string kind, object data);
    (Guid Id, ChannelReader<string> Reader) Join(string room);
    void Leave(Guid id);
}

// Push to the phones (WebPush in Infrastructure). A phone belongs to one room, or to the host ("" = every room).
public interface IPushSender
{
    string PublicKey { get; }
    void Subscribe(string room, string endpoint, string p256dh, string auth, string name);
    void Unsubscribe(string endpoint);
    /// <summary>To the room's phones and the host's phones, except the one that caused it; runs in the background.</summary>
    void SendToOthers(string room, string? exceptEndpoint, string title, string body, string tag, string url);
    void ForgetRoom(string room);
}

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

    public Group(IGroupStore store, ILiveFeed live, IPushSender push, TimeProvider clock, RoomContext room)
    {
        (_store, _live, _push, _clock, _room) = (store, live, push, clock, room);
        _messages = store.LoadMessages();
        _tasks = store.LoadTasks();
        _files = store.LoadFiles();
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

    // ---- tasks (the host makes them, everyone can take one) ----

    public IReadOnlyList<TaskItem> Tasks()
    {
        lock (_gate) return _tasks.Select(GroupRules.Copy).ToList();
    }

    public TaskItem? AddTask(string? title, string? note, string? when, string? fromEndpoint = null)
    {
        TaskItem? t;
        lock (_gate)
        {
            t = GroupRules.NewTask(_tasks, title, note, when, _clock.GetUtcNow().UtcDateTime);
            if (t is null) return null;
            _tasks.Add(t);
            _store.SaveTasks(_tasks);
        }
        TasksChanged();
        _push.SendToOthers(_room.Id, fromEndpoint, "Ny opgave · " + _room.Title, t.Title + (t.When.Length > 0 ? " · " + t.When : ""), "snak-opg-" + _room.Id, _room.Url);
        _room.Activity(false);
        return GroupRules.Copy(t);
    }

    public TaskItem? EditTask(long id, string? title, string? note, string? when) => Change(id, t => GroupRules.Edit(t, title, note, when));

    public TaskItem? SetDone(long id, bool done) => Change(id, t => t with { Done = done });

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
}

// What the host's overview shows per room.
public sealed record RoomSummary(string Id, string Key, string Title, string CustomerRef, DateTime CreatedAt, DateTime LastAt,
    DateTime? DoneAt, DateTime? DeleteAt, bool Waiting, string LastName, string LastText, bool Gruppe);

// All rooms. Martin makes them (or Kunder / itmartin.dk through the API key); the link with the key is the way in.
public sealed class Rooms
{
    private readonly IRoomStore _store;
    private readonly ILiveFeed _live;
    private readonly IPushSender _push;
    private readonly TimeProvider _clock;
    private readonly string _publicUrl;
    private readonly object _gate = new();
    private readonly List<Room> _rooms;
    private readonly ConcurrentDictionary<string, Group> _groups = new();

    public Rooms(IRoomStore store, ILiveFeed live, IPushSender push, TimeProvider clock, string publicUrl)
    {
        (_store, _live, _push, _clock, _publicUrl) = (store, live, push, clock, publicUrl.TrimEnd('/'));
        _rooms = store.LoadRooms();
    }

    public string Link(Room r) => $"{_publicUrl}/r/{r.Key}";

    public Room Create(string? title, string? customerRef = null, bool group = false)
    {
        var r = RoomRules.New(title, customerRef, _clock.GetUtcNow().UtcDateTime, group);
        lock (_gate)
        {
            if (_rooms.Count >= RoomRules.MaxRooms) throw new InvalidOperationException("Der er for mange rum. Slet nogle gamle først.");
            _rooms.Add(r);
            _store.SaveRooms(_rooms);
        }
        return r;
    }

    public Room? ByKey(string? key)
    {
        if (!RoomRules.ValidKey(key)) return null;
        lock (_gate) return _rooms.FirstOrDefault(r => r.Key == key);
    }

    public Room? ById(string? id)
    {
        if (!RoomRules.ValidId(id)) return null;
        lock (_gate) return _rooms.FirstOrDefault(r => r.Id == id);
    }

    public Group GroupOf(Room r) => _groups.GetOrAdd(r.Id, id =>
        new Group(_store.For(id), _live, _push, _clock, new RoomContext(id, r.Title, $"/r/{r.Key}", msg => Touched(id, msg))));

    /// <summary>Newest activity first; rooms waiting for Martin on top.</summary>
    public List<RoomSummary> Overview()
    {
        List<Room> rooms;
        lock (_gate) rooms = _rooms.ToList();
        return rooms.Select(r =>
            {
                var g = GroupOf(r);
                var last = g.Last;
                return new RoomSummary(r.Id, r.Key, r.Title, r.CustomerRef, r.CreatedAt, r.LastAt, r.DoneAt, r.DeleteAt,
                    !r.IsDone && g.Waiting, last?.Name ?? "", last is null ? "" : last.Text.Length > 120 ? last.Text[..120] + "…" : last.Text, r.Gruppe);
            })
            .OrderByDescending(s => s.Waiting).ThenBy(s => s.DoneAt is not null).ThenByDescending(s => s.LastAt).ToList();
    }

    /// <summary>"Sagen er klaret": says so in the room, and the room is deleted 14 days later.</summary>
    public bool MarkDone(string id, string hostName)
    {
        var r = ById(id);
        if (r is null || r.Gruppe) return false;   // a group has no end
        GroupOf(r).Write(hostName, RoomRules.DoneText, host: true);
        Update(id, x => x with { DoneAt = _clock.GetUtcNow().UtcDateTime });
        return true;
    }

    public bool Reopen(string id) => Update(id, x => x with { DoneAt = null });

    public bool Rename(string id, string? title) => Update(id, x => x with { Title = RoomRules.CleanTitle(title) });

    /// <summary>A group's people and topics (Martin pastes them, one per line). Everyone in the room sees the change at once.</summary>
    public bool SetRoster(string id, string? membersText, string? topicsText)
    {
        var members = RosterRules.ParseMembers(membersText);
        var topics = RosterRules.ParseTopics(topicsText);
        if (!Update(id, x => x with { Members = members, Topics = topics })) return false;
        _live.Send(id, "holdet", new { medlemmer = members, emner = topics });
        return true;
    }

    public bool Delete(string id)
    {
        lock (_gate)
        {
            if (_rooms.RemoveAll(r => r.Id == id) == 0) return false;
            _store.SaveRooms(_rooms);
        }
        _groups.TryRemove(id, out _);
        _store.DeleteRoomData(id);
        _push.ForgetRoom(id);
        return true;
    }

    /// <summary>Rooms finished more than 14 days ago are deleted - messages, tasks and phones.</summary>
    public int DeleteFinished()
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        List<string> due;
        lock (_gate) due = _rooms.Where(r => RoomRules.ShouldDelete(r, now)).Select(r => r.Id).ToList();
        foreach (var id in due) Delete(id);
        return due.Count;
    }

    // A new message opens a finished room again (the customer had one more question).
    private void Touched(string id, bool message)
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        Update(id, x =>
        {
            var reopen = message && x.IsDone && now - x.DoneAt!.Value > TimeSpan.FromSeconds(5);
            return x with { LastAt = now, DoneAt = reopen ? null : x.DoneAt };
        });
    }

    private bool Update(string id, Func<Room, Room> f)
    {
        lock (_gate)
        {
            var i = _rooms.FindIndex(r => r.Id == id);
            if (i < 0) return false;
            _rooms[i] = f(_rooms[i]);
            _store.SaveRooms(_rooms);
            return true;
        }
    }
}

public enum LoginResult { Admin, Wrong, Locked }

// Martin's own code (Snak:AdminCode). Rooms need no code - the link is the way in. Ten wrong tries from one address lock
// it for 15 minutes. Snak:ApiKey lets Kunder and itmartin.dk make a room.
public sealed class Access(string adminCode, string apiKey, TimeProvider clock)
{
    private readonly ConcurrentDictionary<string, (int Count, DateTime First)> _wrong = new();

    public string AdminToken { get; } = AccessRules.Token(adminCode, "admin");

    public bool IsAdmin(string? adminCookie) => adminCode.Length >= AccessRules.MinAdminCode && adminCookie == AdminToken;
    public bool IsApi(string? key) => apiKey.Length >= 16 && key == apiKey;

    public LoginResult Login(string? typed, string clientIp)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        if (_wrong.TryGetValue(clientIp, out var w) && w.Count >= AccessRules.MaxWrongTries && now - w.First < AccessRules.WrongTriesWindow)
            return LoginResult.Locked;
        var code = (typed ?? "").Trim();
        if (adminCode.Length >= AccessRules.MinAdminCode && code == adminCode) { _wrong.TryRemove(clientIp, out _); return LoginResult.Admin; }
        _wrong.AddOrUpdate(clientIp, _ => (1, now),
            (_, old) => now - old.First > AccessRules.WrongTriesWindow ? (1, now) : (old.Count + 1, old.First));
        return LoginResult.Wrong;
    }
}

public static class DependencyInjection
{
    /// <summary>The use cases. Ports come from AddSnakInfrastructure.</summary>
    public static IServiceCollection AddSnakApplication(this IServiceCollection services, string adminCode, string apiKey, string publicUrl)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(sp => new Rooms(sp.GetRequiredService<IRoomStore>(), sp.GetRequiredService<ILiveFeed>(),
            sp.GetRequiredService<IPushSender>(), sp.GetRequiredService<TimeProvider>(), publicUrl));
        services.AddSingleton(sp => new Access(adminCode, apiKey, sp.GetRequiredService<TimeProvider>()));
        return services;
    }
}

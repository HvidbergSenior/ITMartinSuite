using System.Collections.Concurrent;
using System.Threading.Channels;
using ITMartinSnak.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace ITMartinSnak.Application;

// The group's messages and tasks on disk (two small JSON files in Infrastructure).
public interface IGroupStore
{
    List<Message> LoadMessages();
    void SaveMessages(IReadOnlyList<Message> messages);
    List<TaskItem> LoadTasks();
    void SaveTasks(IReadOnlyList<TaskItem> tasks);
}

// Everyone with the page open gets each change at once (server-sent events in Infrastructure).
public interface ILiveFeed
{
    void Send(string kind, object data);
    (Guid Id, ChannelReader<string> Reader) Join();
    void Leave(Guid id);
}

// Push to the phones (WebPush in Infrastructure).
public interface IPushSender
{
    string PublicKey { get; }
    void Subscribe(string endpoint, string p256dh, string auth, string name);
    void Unsubscribe(string endpoint);
    /// <summary>To every phone except the one that caused it; runs in the background.</summary>
    void SendToOthers(string? exceptEndpoint, string title, string body, string tag);
}

// The chat and the tasks. A few friends write a few hundred messages a month, so everything is held in memory and written
// to disk on each change. Every change goes live to the open pages and as a push to the other phones.
public sealed class Group
{
    private readonly IGroupStore _store;
    private readonly ILiveFeed _live;
    private readonly IPushSender _push;
    private readonly TimeProvider _clock;
    private readonly object _gate = new();
    private readonly List<Message> _messages;
    private readonly List<TaskItem> _tasks;

    public Group(IGroupStore store, ILiveFeed live, IPushSender push, TimeProvider clock)
    {
        (_store, _live, _push, _clock) = (store, live, push, clock);
        _messages = store.LoadMessages();
        _tasks = store.LoadTasks();
    }

    // ---- chat ----

    public IReadOnlyList<Message> MessagesAfter(long afterId, int max = 300)
    {
        lock (_gate) return _messages.Where(m => m.Id > afterId).TakeLast(max).ToList();
    }

    public Message? Write(string? name, string? text, string? fromEndpoint = null)
    {
        Message? m;
        lock (_gate)
        {
            m = GroupRules.NewMessage(_messages, name, text, _clock.GetUtcNow().UtcDateTime);
            if (m is null) return null;
            _messages.Add(m);
            GroupRules.Trim(_messages);
            _store.SaveMessages(_messages);
        }
        _live.Send("besked", m);
        _push.SendToOthers(fromEndpoint, m.Name, m.Text, "snak-besked");
        return m;
    }

    // ---- tasks (the admin makes them, everyone can take one) ----

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
        _push.SendToOthers(fromEndpoint, "Ny opgave", t.Title + (t.When.Length > 0 ? " · " + t.When : ""), "snak-opgave");
        return GroupRules.Copy(t);
    }

    public TaskItem? EditTask(long id, string? title, string? note, string? when) => Change(id, t => GroupRules.Edit(t, title, note, when));

    public TaskItem? SetDone(long id, bool done) => Change(id, t => t with { Done = done });

    public TaskItem? ToggleTaker(long id, string? name, string? fromEndpoint = null)
    {
        var t = Change(id, x => GroupRules.ToggleTaker(x, name));
        if (t is not null && GroupRules.HasTaker(t, name))
            _push.SendToOthers(fromEndpoint, "Opgave taget", $"{GroupRules.CleanName(name)} tager: {t.Title}", "snak-opgave");
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

    private void TasksChanged() => _live.Send("opgaver", Tasks());
}

public enum LoginResult { Member, Admin, Wrong, Locked }

// Access with the group code / admin code (Snak:Code, Snak:AdminCode). Ten wrong tries from one address lock it for 15 minutes.
public sealed class Access(string groupCode, string adminCode, TimeProvider clock)
{
    private readonly ConcurrentDictionary<string, (int Count, DateTime First)> _wrong = new();

    public string MemberToken { get; } = AccessRules.Token(groupCode, "medlem");
    public string AdminToken { get; } = AccessRules.Token(adminCode, "admin");

    public bool IsAdmin(string? adminCookie) => adminCode.Length >= AccessRules.MinAdminCode && adminCookie == AdminToken;
    public bool IsMember(string? memberCookie, string? adminCookie) =>
        IsAdmin(adminCookie) || (groupCode.Length >= AccessRules.MinGroupCode && memberCookie == MemberToken);

    public LoginResult Login(string? typed, string clientIp)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        if (_wrong.TryGetValue(clientIp, out var w) && w.Count >= AccessRules.MaxWrongTries && now - w.First < AccessRules.WrongTriesWindow)
            return LoginResult.Locked;
        var code = (typed ?? "").Trim();
        if (adminCode.Length >= AccessRules.MinAdminCode && code == adminCode) { _wrong.TryRemove(clientIp, out _); return LoginResult.Admin; }
        if (groupCode.Length >= AccessRules.MinGroupCode && code == groupCode) { _wrong.TryRemove(clientIp, out _); return LoginResult.Member; }
        _wrong.AddOrUpdate(clientIp, _ => (1, now),
            (_, old) => now - old.First > AccessRules.WrongTriesWindow ? (1, now) : (old.Count + 1, old.First));
        return LoginResult.Wrong;
    }
}

public static class DependencyInjection
{
    /// <summary>The use cases. Ports come from AddSnakInfrastructure.</summary>
    public static IServiceCollection AddSnakApplication(this IServiceCollection services, string groupCode, string adminCode)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<Group>();
        services.AddSingleton(sp => new Access(groupCode, adminCode, sp.GetRequiredService<TimeProvider>()));
        return services;
    }
}

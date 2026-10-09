using System.Text.Json;

namespace ITMartinSnak.Server;

public sealed record Message(long Id, string Name, string Text, DateTime At);

public sealed record TaskItem(long Id, string Title, string Note, string When, List<string> Takers, bool Done, DateTime CreatedAt);

/// <summary>
/// The group's messages and tasks, kept in two small JSON files in the data folder. A few friends
/// write a few hundred messages a month, so a file per list is plenty; only the newest
/// <see cref="MaxMessages"/> messages are kept.
/// </summary>
public sealed class Store
{
    public const int MaxMessages = 5000;
    public const int MaxText = 4000;
    public const int MaxName = 30;

    private readonly string _msgFile, _taskFile;
    private readonly object _gate = new();
    private readonly TimeProvider _clock;
    private readonly List<Message> _messages;
    private readonly List<TaskItem> _tasks;

    public Store(string dataDir, TimeProvider clock)
    {
        _clock = clock;
        Directory.CreateDirectory(dataDir);
        _msgFile = Path.Combine(dataDir, "messages.json");
        _taskFile = Path.Combine(dataDir, "tasks.json");
        _messages = Load<List<Message>>(_msgFile) ?? [];
        _tasks = Load<List<TaskItem>>(_taskFile) ?? [];
    }

    private static T? Load<T>(string file)
    {
        if (!File.Exists(file)) return default;
        try { return JsonSerializer.Deserialize<T>(File.ReadAllText(file)); } catch { return default; }
    }

    private static void Save<T>(string file, T data)
    {
        var tmp = file + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(data));
        File.Move(tmp, file, true);
    }

    public static string CleanName(string? name) => Cut((name ?? "").Trim(), MaxName);

    private static string Cut(string s, int max) => s.Length > max ? s[..max] : s;

    // ---- messages ----

    public IReadOnlyList<Message> MessagesAfter(long afterId, int max = 300)
    {
        lock (_gate) return _messages.Where(m => m.Id > afterId).TakeLast(max).ToList();
    }

    public Message? AddMessage(string? name, string? text)
    {
        var n = CleanName(name);
        var t = Cut((text ?? "").Trim(), MaxText);
        if (n.Length == 0 || t.Length == 0) return null;
        lock (_gate)
        {
            var m = new Message((_messages.Count == 0 ? 0 : _messages[^1].Id) + 1, n, t, _clock.GetUtcNow().UtcDateTime);
            _messages.Add(m);
            if (_messages.Count > MaxMessages) _messages.RemoveRange(0, _messages.Count - MaxMessages);
            Save(_msgFile, _messages);
            return m;
        }
    }

    // ---- tasks (the admin makes them, everyone can take one) ----

    public IReadOnlyList<TaskItem> Tasks()
    {
        lock (_gate) return _tasks.Select(Copy).ToList();
    }

    private static TaskItem Copy(TaskItem t) => t with { Takers = t.Takers.ToList() };

    public TaskItem? AddTask(string? title, string? note, string? when)
    {
        var ti = (title ?? "").Trim();
        if (ti.Length == 0) return null;
        lock (_gate)
        {
            var t = new TaskItem((_tasks.Count == 0 ? 0 : _tasks.Max(x => x.Id)) + 1, Cut(ti, 120), Cut((note ?? "").Trim(), 1000),
                Cut((when ?? "").Trim(), 60), [], false, _clock.GetUtcNow().UtcDateTime);
            _tasks.Add(t);
            Save(_taskFile, _tasks);
            return Copy(t);
        }
    }

    public TaskItem? EditTask(long id, string? title, string? note, string? when)
    {
        var ti = (title ?? "").Trim();
        if (ti.Length == 0) return null;
        return Change(id, t => t with { Title = Cut(ti, 120), Note = Cut((note ?? "").Trim(), 1000), When = Cut((when ?? "").Trim(), 60) });
    }

    /// <summary>Puts the name on the task, or takes it off again if it is already there.</summary>
    public TaskItem? ToggleTaker(long id, string? name)
    {
        var n = CleanName(name);
        if (n.Length == 0) return null;
        return Change(id, t =>
        {
            var takers = t.Takers.ToList();
            var i = takers.FindIndex(x => string.Equals(x, n, StringComparison.OrdinalIgnoreCase));
            if (i >= 0) takers.RemoveAt(i); else takers.Add(n);
            return t with { Takers = takers };
        });
    }

    public TaskItem? SetDone(long id, bool done) => Change(id, t => t with { Done = done });

    public bool DeleteTask(long id)
    {
        lock (_gate)
        {
            if (_tasks.RemoveAll(t => t.Id == id) == 0) return false;
            Save(_taskFile, _tasks);
            return true;
        }
    }

    private TaskItem? Change(long id, Func<TaskItem, TaskItem> f)
    {
        lock (_gate)
        {
            var i = _tasks.FindIndex(t => t.Id == id);
            if (i < 0) return null;
            _tasks[i] = f(_tasks[i]);
            Save(_taskFile, _tasks);
            return Copy(_tasks[i]);
        }
    }
}

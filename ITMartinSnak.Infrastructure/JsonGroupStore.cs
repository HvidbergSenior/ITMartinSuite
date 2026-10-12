using System.Text.Json;
using ITMartinSnak.Application;
using ITMartinSnak.Domain;

namespace ITMartinSnak.Infrastructure;

/// <summary>
/// The group's messages and tasks in two small JSON files in the data folder (Snak:DataDir). A few friends write a few
/// hundred messages a month, so a file per list is plenty. Written via a temp file, so a crash never leaves half a file.
/// </summary>
public sealed class JsonGroupStore : IGroupStore
{
    private readonly string _msgFile, _taskFile;

    public JsonGroupStore(string dataDir)
    {
        Directory.CreateDirectory(dataDir);
        _msgFile = Path.Combine(dataDir, "messages.json");
        _taskFile = Path.Combine(dataDir, "tasks.json");
    }

    public List<Message> LoadMessages() => Load<List<Message>>(_msgFile) ?? [];
    public void SaveMessages(IReadOnlyList<Message> messages) => Save(_msgFile, messages);
    public List<TaskItem> LoadTasks() => Load<List<TaskItem>>(_taskFile) ?? [];
    public void SaveTasks(IReadOnlyList<TaskItem> tasks) => Save(_taskFile, tasks);

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
}

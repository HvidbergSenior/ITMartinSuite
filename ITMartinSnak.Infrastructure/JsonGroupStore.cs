using System.Text.Json;
using ITMartinSnak.Application;
using ITMartinSnak.Domain;

namespace ITMartinSnak.Infrastructure;

/// <summary>
/// One room's messages and tasks in two small JSON files. Written via a temp file, so a crash never leaves half a file.
/// </summary>
public sealed class JsonGroupStore : IGroupStore
{
    private readonly string _msgFile, _taskFile;

    public JsonGroupStore(string dir)
    {
        Directory.CreateDirectory(dir);
        _msgFile = Path.Combine(dir, "messages.json");
        _taskFile = Path.Combine(dir, "tasks.json");
    }

    public List<Message> LoadMessages() => Json.Load<List<Message>>(_msgFile) ?? [];
    public void SaveMessages(IReadOnlyList<Message> messages) => Json.Save(_msgFile, messages);
    public List<TaskItem> LoadTasks() => Json.Load<List<TaskItem>>(_taskFile) ?? [];
    public void SaveTasks(IReadOnlyList<TaskItem> tasks) => Json.Save(_taskFile, tasks);
}

/// <summary>
/// rooms.json + a folder per room (rooms/&lt;id&gt;/messages.json, tasks.json) in the data folder (Snak:DataDir).
/// The first start after 2026-11-04 moves the old single group (messages.json/tasks.json in the data folder) into the room
/// "Rico og venner" - nothing is deleted.
/// </summary>
public sealed class JsonRoomStore : IRoomStore
{
    private readonly string _dataDir, _roomsFile, _roomsDir;

    public JsonRoomStore(string dataDir, TimeProvider clock)
    {
        _dataDir = dataDir;
        Directory.CreateDirectory(dataDir);
        _roomsFile = Path.Combine(dataDir, "rooms.json");
        _roomsDir = Path.Combine(dataDir, "rooms");
        Directory.CreateDirectory(_roomsDir);
        MoveOldGroup(clock.GetUtcNow().UtcDateTime);
    }

    public List<Room> LoadRooms() => Json.Load<List<Room>>(_roomsFile) ?? [];
    public void SaveRooms(IReadOnlyList<Room> rooms) => Json.Save(_roomsFile, rooms);
    public IGroupStore For(string roomId) => new JsonGroupStore(Dir(roomId));

    public void DeleteRoomData(string roomId)
    {
        var d = Dir(roomId);
        if (Directory.Exists(d)) Directory.Delete(d, true);
    }

    // Room ids are checked by RoomRules.ValidId (a-z0-9), so the folder always stays inside rooms/.
    private string Dir(string roomId) =>
        RoomRules.ValidId(roomId) ? Path.Combine(_roomsDir, roomId) : throw new ArgumentException("Bad room id", nameof(roomId));

    private void MoveOldGroup(DateTime nowUtc)
    {
        var oldMsgs = Path.Combine(_dataDir, "messages.json");
        var oldTasks = Path.Combine(_dataDir, "tasks.json");
        if (File.Exists(_roomsFile) || (!File.Exists(oldMsgs) && !File.Exists(oldTasks))) return;
        var dir = Dir(RoomRules.LegacyRoomId);
        Directory.CreateDirectory(dir);
        if (File.Exists(oldMsgs)) File.Move(oldMsgs, Path.Combine(dir, "messages.json"), true);
        if (File.Exists(oldTasks)) File.Move(oldTasks, Path.Combine(dir, "tasks.json"), true);
        var last = (Json.Load<List<Message>>(Path.Combine(dir, "messages.json")) ?? []).LastOrDefault()?.At ?? nowUtc;
        SaveRooms([new Room(RoomRules.LegacyRoomId, RoomRules.NewKey(), "Rico og venner", nowUtc, last, null, "")]);
    }
}

internal static class Json
{
    public static T? Load<T>(string file)
    {
        if (!File.Exists(file)) return default;
        try { return JsonSerializer.Deserialize<T>(File.ReadAllText(file)); } catch { return default; }
    }

    public static void Save<T>(string file, T data)
    {
        var tmp = file + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(data));
        File.Move(tmp, file, true);
    }
}

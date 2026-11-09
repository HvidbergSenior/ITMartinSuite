using System.Text.Json;
using ITMartinSnak.Application;
using ITMartinSnak.Domain;

namespace ITMartinSnak.Infrastructure;

/// <summary>
/// One room's messages and tasks in two small JSON files. Written via a temp file, so a crash never leaves half a file.
/// </summary>
public sealed partial class JsonGroupStore : IGroupStore
{
    private readonly string _msgFile, _taskFile, _filesFile, _filesDir;

    public JsonGroupStore(string dir)
    {
        Directory.CreateDirectory(dir);
        _msgFile = Path.Combine(dir, "messages.json");
        _taskFile = Path.Combine(dir, "tasks.json");
        _filesFile = Path.Combine(dir, "files.json");
        _filesDir = Path.Combine(dir, "files");
    }

    public List<Message> LoadMessages() => Json.Load<List<Message>>(_msgFile) ?? [];
    public void SaveMessages(IReadOnlyList<Message> messages) => Json.Save(_msgFile, messages);
    public List<TaskItem> LoadTasks() => Json.Load<List<TaskItem>>(_taskFile) ?? [];
    public void SaveTasks(IReadOnlyList<TaskItem> tasks) => Json.Save(_taskFile, tasks);
    public List<SharedFile> LoadFiles() => Json.Load<List<SharedFile>>(_filesFile) ?? [];
    public void SaveFiles(IReadOnlyList<SharedFile> files) => Json.Save(_filesFile, files);

    // Stored names are "<id>.<ext>" from FileRules.New - checked again here, so a path never leaves files/.
    [System.Text.RegularExpressions.GeneratedRegex(@"^[0-9]{1,9}\.[a-z0-9]{1,5}$")]
    private static partial System.Text.RegularExpressions.Regex StoredShape();

    private string Content(string storedName) =>
        StoredShape().IsMatch(storedName) ? Path.Combine(_filesDir, storedName) : throw new ArgumentException("Bad file name", nameof(storedName));

    public async Task<bool> WriteContentAsync(string storedName, Stream content, long maxBytes, CancellationToken ct)
    {
        Directory.CreateDirectory(_filesDir);
        var file = Content(storedName);
        var tmp = file + ".tmp";
        long total = 0;
        try
        {
            await using (var output = File.Create(tmp))
            {
                var buf = new byte[81920];
                int n;
                while ((n = await content.ReadAsync(buf, ct)) > 0)
                {
                    total += n;
                    if (total > maxBytes) break;
                    await output.WriteAsync(buf.AsMemory(0, n), ct);
                }
            }
            if (total == 0 || total > maxBytes) { File.Delete(tmp); return false; }
            File.Move(tmp, file, true);
            return true;
        }
        catch
        {
            try { File.Delete(tmp); } catch { }
            throw;
        }
    }

    public Stream? OpenContent(string storedName)
    {
        var f = Content(storedName);
        return File.Exists(f) ? File.OpenRead(f) : null;
    }

    public void DeleteContent(string storedName)
    {
        var f = Content(storedName);
        if (File.Exists(f)) File.Delete(f);
    }
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

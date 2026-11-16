using System.Threading.Channels;
using ITMartinSnak.Domain;

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
    List<Seen> LoadSeen();
    Dictionary<string, List<long>> LoadMine();
    List<Profile> LoadProfiles();
    void SaveProfiles(IReadOnlyList<Profile> profiles);
    void SaveMine(IReadOnlyDictionary<string, List<long>> mine);
    void SaveSeen(IReadOnlyList<Seen> seen);
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
    /// <summary>Per name: how many phones get beskeder from this room (for the setup view).</summary>
    IReadOnlyDictionary<string, int> PhonesPerName(string room);
}

// Plain-text mail to a customer (SMTP in Infrastructure). False = not sent (no password set, or the mail server said no).
public interface IRoomMailer
{
    Task<bool> SendAsync(string to, string subject, string body, CancellationToken ct = default);
}

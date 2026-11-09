using System.Collections.Concurrent;
using ITMartinSnak.Domain;

namespace ITMartinSnak.Application;

// What the host's overview shows per room.
public sealed record RoomSummary(string Id, string Key, string Title, string CustomerRef, DateTime CreatedAt, DateTime LastAt,
    DateTime? DoneAt, DateTime? DeleteAt, bool Waiting, string LastName, string LastText, IReadOnlyList<string> Members, bool HasCode, string Slug,
    bool Gruppe);

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

    public string? EasyLink(Room r) => r.Slug.Length > 0 ? $"{_publicUrl}/{r.Slug}" : null;

    public Room Create(string? title, string? customerRef = null, string? members = null, string? adminCode = null, string? slug = null,
        bool group = false)
    {
        var r = RoomRules.New(title, customerRef, _clock.GetUtcNow().UtcDateTime, members, adminCode, slug, group);
        lock (_gate)
        {
            if (r.Slug.Length > 0 && _rooms.Any(x => x.Slug == r.Slug)) throw new InvalidOperationException($"Adressen /{r.Slug} er allerede brugt.");
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

    public Room? BySlug(string? slug)
    {
        var s = RoomRules.CleanSlug(slug);
        if (s.Length == 0) return null;
        lock (_gate) return _rooms.FirstOrDefault(r => r.Slug == s);
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
                    !r.IsDone && g.Waiting, last?.Name ?? "", last is null ? "" : last.Text.Length > 120 ? last.Text[..120] + "…" : last.Text,
                    r.Members ?? [], r.HasCode, r.Slug, r.Gruppe);
            })
            .OrderByDescending(s => s.Waiting).ThenBy(s => s.DoneAt is not null).ThenByDescending(s => s.LastAt).ToList();
    }

    /// <summary>Every minute: reminders before today's tasks, in every open room.</summary>
    public int SendReminders()
    {
        List<Room> rooms;
        lock (_gate) rooms = _rooms.Where(r => !r.IsDone).ToList();
        return rooms.Sum(r => GroupOf(r).SendReminders());
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
    public bool SetRoster(string id, string? membersText, string? topicsText, string? linksText = null)
    {
        var people = RosterRules.ParseMembers(membersText);
        var topics = RosterRules.ParseTopics(topicsText);
        // Members stays the plain name list (door buttons, "give the task to"); About holds what each one works with.
        var names = people.Select(m => m.Name).ToList();
        var about = people.Where(m => m.About.Length > 0).ToDictionary(m => m.Name, m => m.About);
        Room? after = null;
        if (!Update(id, x => after = x with { Members = names, About = about, Topics = topics,
                Links = linksText is null ? x.Links : RosterRules.ParseLinks(linksText) })) return false;
        _live.Send(id, "holdet", new { medlemmer = names, om = about, emner = topics, links = after?.Links ?? [] });
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

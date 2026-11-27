using ITMartinMode.Domain;

namespace ITMartinMode.Application;

public interface IBoardStore
{
    Board Load(string slug);
    void Save(string slug, Board board);
}

public sealed record RoomSetup(string Slug, string Title, string Key);

public sealed record PersonView(string Id, string Name, string Path);
public sealed record NoteView(string Id, string Point, string Text, string Author, string IdeaOf);
public sealed record GameView(int Round, int Rounds, int Step, string? Point, string OwnerId, string OwnerName,
    string SpeakerId, string SpeakerName, int? LeftMs, bool Finished);
public sealed record RoomView(string Title, string Idea, IReadOnlyList<PersonView> People, IReadOnlyList<NoteView> Notes, GameView? Game);

/// <summary>All rooms: people in memory, the board in the store. One lock - a handful of people.</summary>
public sealed class MeetingService
{
    private readonly Dictionary<string, Room> _rooms;
    private readonly IBoardStore _store;
    private readonly Func<DateTimeOffset> _now;
    private readonly object _gate = new();

    /// <summary>Raised with the room's slug after every change, so the live pages update.</summary>
    public event Action<string>? Changed;

    public MeetingService(IEnumerable<RoomSetup> rooms, IBoardStore store, Func<DateTimeOffset>? now = null)
    {
        _store = store;
        _now = now ?? (() => DateTimeOffset.UtcNow);
        _rooms = rooms.Where(r => r.Slug.Length > 0 && r.Key.Length > 0)
            .Select(r => r with { Slug = r.Slug.ToLowerInvariant() })
            .ToDictionary(r => r.Slug, r => new Room(r.Slug, r.Title, r.Key, store.Load(r.Slug)));
    }

    public Room? Door(string? slug, string? key) =>
        _rooms.TryGetValue((slug ?? "").ToLowerInvariant(), out var r) && r.KeyOk(key) ? r : null;

    public bool Exists(string? slug) => _rooms.ContainsKey((slug ?? "").ToLowerInvariant());

    public Participant Join(Room room, string? name)
    {
        Participant p;
        lock (_gate) p = room.Join(name, _now());
        Changed?.Invoke(room.Slug);
        return p;
    }

    public bool Alive(Room room, string? id, string? token)
    {
        lock (_gate)
        {
            var p = room.Find(id, token);
            if (p is null) return false;
            p.LastSeen = _now();
            return true;
        }
    }

    public void Leave(Room room, string? id, string? token)
    {
        lock (_gate) { var p = room.Find(id, token); if (p is null) return; room.Leave(p.Id); }
        Changed?.Invoke(room.Slug);
    }

    /// <summary>Only the person themself may publish to their own video path.</summary>
    public bool CanPublish(Room room, string? id, string? token, string path)
    {
        lock (_gate) { var p = room.Find(id, token); return p is not null && p.MediaPath(room.Slug) == path; }
    }

    public void AddNote(Room room, string? id, string? token, StarPoint point, string? text) =>
        Edit(room, id, token, p => room.Board.Add(point, text, p.Name, _now(), room.Game is { Finished: false } g ? g.Owner.Name : null));

    public void RemoveNote(Room room, string? id, string? token, string noteId) =>
        Edit(room, id, token, _ => room.Board.Remove(noteId));

    public void SetIdea(Room room, string? id, string? token, string? idea) =>
        Edit(room, id, token, _ => room.Board.SetIdea(idea));

    public void ClearBoard(Room room, string? id, string? token) =>
        Edit(room, id, token, _ => room.Board.Clear());

    public void StartGame(Room room, string? id, string? token) => Edit(room, id, token, _ => room.StartGame(_now()), save: false);
    public void NextTurn(Room room, string? id, string? token) => Edit(room, id, token, _ => room.Game?.Next(_now()), save: false);
    public void StopGame(Room room, string? id, string? token) => Edit(room, id, token, _ => room.StopGame(), save: false);

    private void Edit(Room room, string? id, string? token, Action<Participant> change, bool save = true)
    {
        lock (_gate)
        {
            var p = room.Find(id, token) ?? throw new DomainException("Gå ind i rummet igen.");
            change(p);
            if (save) _store.Save(room.Slug, room.Board);
        }
        Changed?.Invoke(room.Slug);
    }

    public RoomView View(Room room)
    {
        lock (_gate)
            return new RoomView(room.Title, room.Board.Idea,
                room.People.Select(p => new PersonView(p.Id, p.Name, p.MediaPath(room.Slug))).ToList(),
                room.Board.Notes.Select(n => new NoteView(n.Id, n.Point.ToString(), n.Text, n.Author, n.IdeaOf)).ToList(),
                room.Game is { } g
                    ? new GameView(g.Round, g.Players.Count, g.Step, g.Point?.ToString(), g.Finished ? "" : g.Owner.Id, g.Finished ? "" : g.Owner.Name,
                        g.Finished ? "" : g.Speaker.Id, g.Finished ? "" : g.Speaker.Name, g.Left(_now()) is { } l ? (int)l.TotalMilliseconds : null, g.Finished)
                    : null);
    }

    /// <summary>Called every few seconds: people who vanished are removed and the others told.</summary>
    public void SweepAll()
    {
        List<string> changed;
        lock (_gate) changed = _rooms.Values.Where(r => r.Sweep(_now())).Select(r => r.Slug).ToList();
        foreach (var s in changed) Changed?.Invoke(s);
    }
}

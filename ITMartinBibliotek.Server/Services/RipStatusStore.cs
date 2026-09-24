using System.Text.Json;

namespace ITMartinBibliotek.Server.Services;

// One disc going through the rip station on the PC (ripstation.ps1).
// The station owns the job and posts the whole record on every change.
public sealed class RipJob
{
    public string Id { get; set; } = "";
    public string Kind { get; set; } = "";          // "cd" | "dvd" | "bluray"
    public string Label { get; set; } = "";         // disc volume label as the drive reports it
    public string Title { get; set; } = "";         // what it was saved as
    public string Status { get; set; } = "";        // ripping | copying | converting | finishing | done | failed
    public string Step { get; set; } = "";          // plain-Danish line shown under the title
    public int Progress { get; set; }               // 0-100 for the current step
    public string Target { get; set; } = "";        // NAS folder
    public bool NeedsName { get; set; }             // name guessed from the disc label - check it
    public string Error { get; set; } = "";
    public DateTime StartedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

// Status lives in a small JSON file next to the database: the station posts a
// few times a minute, there is one writer, and nothing else needs to query it.
public sealed class RipStatusStore
{
    private const int Keep = 300;
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    private readonly string _path;
    private readonly object _lock = new();
    private State _state;

    public RipStatusStore(IConfiguration config)
    {
        _path = config["Bibliotek:RipStatusFile"] ?? "/app/data/rip-status.json";
        try { _state = File.Exists(_path) ? JsonSerializer.Deserialize<State>(File.ReadAllText(_path)) ?? new() : new(); }
        catch { _state = new(); }
    }

    public event Action? Changed;

    public DateTime? LastSeen { get { lock (_lock) return _state.LastSeen; } }

    public List<RipJob> Jobs() { lock (_lock) return _state.Jobs.OrderByDescending(j => j.StartedAt).ToList(); }

    public void Heartbeat()
    {
        lock (_lock) { _state.LastSeen = DateTime.UtcNow; Save(); }
        Changed?.Invoke();
    }

    public void Upsert(RipJob job)
    {
        lock (_lock)
        {
            job.UpdatedAt = DateTime.UtcNow;
            if (job.StartedAt == default) job.StartedAt = job.UpdatedAt;
            _state.Jobs.RemoveAll(j => j.Id == job.Id);
            _state.Jobs.Add(job);
            if (_state.Jobs.Count > Keep)
                _state.Jobs = _state.Jobs.OrderByDescending(j => j.StartedAt).Take(Keep).ToList();
            _state.LastSeen = job.UpdatedAt;
            Save();
        }
        Changed?.Invoke();
    }

    public void Remove(string id)
    {
        lock (_lock) { _state.Jobs.RemoveAll(j => j.Id == id); Save(); }
        Changed?.Invoke();
    }

    private void Save()
    {
        var tmp = _path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(_state, Json));
        File.Move(tmp, _path, overwrite: true);
    }

    private sealed class State
    {
        public DateTime? LastSeen { get; set; }
        public List<RipJob> Jobs { get; set; } = [];
    }
}

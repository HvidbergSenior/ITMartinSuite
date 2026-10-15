using System.Text.Json;
using ITMartinBibliotek.Application;
using Microsoft.Extensions.Configuration;

namespace ITMartinBibliotek.Infrastructure;

// Status lives in a small JSON file next to the database: the station posts a
// few times a minute, there is one writer, and nothing else needs to query it.
public sealed class RipStatusStore : IRipStatus
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

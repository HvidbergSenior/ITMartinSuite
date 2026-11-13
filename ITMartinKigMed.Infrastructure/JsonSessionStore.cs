using System.Text.Json;
using ITMartinKigMed.Application;
using ITMartinKigMed.Domain;
using Microsoft.Extensions.Logging;

namespace ITMartinKigMed.Infrastructure;

// <data>/session.json is saved on every change, so a restart does not lose the session;
// an ended session goes to <data>/sessions/<start>.json.
public sealed class JsonSessionStore : ISessionStore
{
    private readonly string _file, _archive;
    private readonly ILogger<JsonSessionStore> _log;

    public JsonSessionStore(string dataDir, ILogger<JsonSessionStore> log)
    {
        _log = log;
        _archive = Path.Combine(dataDir, "sessions");
        Directory.CreateDirectory(_archive);
        _file = Path.Combine(dataDir, "session.json");
    }

    public LiveSession Load()
    {
        try { return File.Exists(_file) ? JsonSerializer.Deserialize<LiveSession>(File.ReadAllText(_file)) ?? new() : new(); }
        catch (Exception ex) { _log.LogWarning(ex, "session.json unreadable - starting empty"); return new(); }
    }

    public void Save(LiveSession session) => File.WriteAllText(_file, JsonSerializer.Serialize(session));

    public void DeleteArchives()
    {
        foreach (var f in Directory.EnumerateFiles(_archive, "*.json")) File.Delete(f);
    }
}

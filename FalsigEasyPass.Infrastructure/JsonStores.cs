using System.Text.Json;
using System.Text.Json.Serialization;
using FalsigEasyPass.Application;
using FalsigEasyPass.Domain;

namespace FalsigEasyPass.Infrastructure;

/// <summary>
/// One JSON file per list in the data folder, kept in memory. ~6,000 places is a few MB - simpler than a database and
/// easy to back up. Writes go to a temp file first and are then moved over the old one, so a crash never leaves half a file.
/// </summary>
internal sealed class JsonList<T>(string path, Func<T, string> id) where T : class
{
    private static readonly JsonSerializerOptions Opt = new() { WriteIndented = false, Converters = { new JsonStringEnumConverter() } };
    private readonly object _lock = new();
    private Dictionary<string, T>? _items;

    private Dictionary<string, T> Items
    {
        get
        {
            if (_items is not null) return _items;
            var list = File.Exists(path) ? JsonSerializer.Deserialize<List<T>>(File.ReadAllText(path), Opt) ?? [] : [];
            return _items = list.ToDictionary(id);
        }
    }

    public IReadOnlyList<T> All() { lock (_lock) return Items.Values.ToList(); }
    public T? Get(string key) { lock (_lock) return Items.GetValueOrDefault(key); }
    public bool Any() { lock (_lock) return Items.Count > 0; }

    public void Put(IEnumerable<T> items)
    {
        lock (_lock)
        {
            foreach (var x in items) Items[id(x)] = x;
            Save();
        }
    }

    public bool Remove(string key)
    {
        lock (_lock)
        {
            if (!Items.Remove(key)) return false;
            Save();
            return true;
        }
    }

    private void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(Items.Values.ToList(), Opt));
        File.Move(tmp, path, overwrite: true);
    }
}

internal sealed class JsonStedStore(string dir) : IStedStore
{
    private readonly JsonList<Sted> _list = new(Path.Combine(dir, "steder.json"), s => s.Id);

    public IReadOnlyList<Sted> Alle() => _list.All();
    public Sted? Hent(string id) => _list.Get(id);
    public void Gem(Sted sted) => _list.Put([sted]);
    public void GemMange(IEnumerable<Sted> steder) => _list.Put(steder);
    public bool Slet(string id) => _list.Remove(id);
    public bool Tom => !_list.Any();
}

internal sealed class JsonTilbagemeldingStore(string dir) : ITilbagemeldingStore
{
    private readonly JsonList<Tilbagemelding> _list = new(Path.Combine(dir, "tilbagemeldinger.json"), t => t.Id);

    public IReadOnlyList<Tilbagemelding> Alle() => _list.All();
    public void Gem(Tilbagemelding t) => _list.Put([t]);
}

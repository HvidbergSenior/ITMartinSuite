using System.Text.Json;
using ITMartinR6Assistant.Application;
using ITMartinR6Assistant.Domain;

namespace ITMartinR6Assistant.Infrastructure;

public class R6DataService : IR6DataService
{
    private readonly string _path = Path.Combine(AppContext.BaseDirectory, "Data", "r6data.json");
    private R6GameData? _cache;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public event Action? OnDataChanged;

    private async Task<R6GameData> LoadAsync()
    {
        if (_cache is not null) return _cache;

        await _lock.WaitAsync();
        try
        {
            if (_cache is not null) return _cache;

            var json = await File.ReadAllTextAsync(_path);
            _cache = JsonSerializer.Deserialize<R6GameData>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new R6GameData();
            return _cache;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<R6GameData> GetData() => await LoadAsync();

    public async Task<List<R6Map>> GetMaps()
    {
        var data = await LoadAsync();
        return data.Maps;
    }

    public async Task<R6Map?> GetMap(string name)
    {
        var data = await LoadAsync();
        return data.Maps.FirstOrDefault(m =>
            string.Equals(m.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<BombSite?> GetSite(string mapName, string siteName)
    {
        var map = await GetMap(mapName);
        return map?.Sites.FirstOrDefault(s =>
            string.Equals(s.Name, siteName, StringComparison.OrdinalIgnoreCase));
    }

    // Same file the app reads from is already the writable, volume-mounted
    // Data folder on the real deployment - no separate "editable copy" needed.
    public async Task SaveAsync(R6GameData data)
    {
        await _lock.WaitAsync();
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            await File.WriteAllTextAsync(_path, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
            _cache = data;
        }
        finally
        {
            _lock.Release();
        }
        OnDataChanged?.Invoke();
    }
}

using ITMartinR6Intel.Server.Models;

namespace ITMartinR6Intel.Server.Services;

public class IntelDataService
{
    private readonly IntelGameData _data;

    public IntelDataService(IWebHostEnvironment env)
    {
        var path = Path.Combine(env.ContentRootPath, "Data", "maps.json");
        _data = System.Text.Json.JsonSerializer.Deserialize<IntelGameData>(
            File.ReadAllText(path),
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }
        ) ?? new IntelGameData();
    }

    public List<IntelMap> GetMaps() => _data.Maps;
    public IntelMap? GetMap(string name) => _data.Maps.FirstOrDefault(m => m.Name == name);
}

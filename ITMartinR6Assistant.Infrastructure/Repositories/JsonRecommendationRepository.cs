using System.Text.Json;
using ITMartinR6Assistant.Application;
using ITMartinR6Assistant.Domain.Entities;

namespace ITMartinR6Assistant.Infrastructure.Repositories;

public class JsonRecommendationRepository : IRecommendationRepository
{
    private readonly string _dataPath;

    public JsonRecommendationRepository()
    {
        _dataPath = "/app/Data";
    }

    public Task<List<string>> GetMaps()
    {
        var maps = Directory
            .GetFiles(_dataPath, "*.json")
            .Select(Path.GetFileNameWithoutExtension)
            .ToList();

        return Task.FromResult(maps);
    }

    public async Task<List<SiteRecommendation>> GetRecommendations(string map)
    {
        var file = Path.Combine(_dataPath, $"{map}.json");

        if (!File.Exists(file))
        {
            return [];
        }

        var json = await File.ReadAllTextAsync(file);

        var result = JsonSerializer.Deserialize<List<SiteRecommendation>>(
            json,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

        return result ?? [];
    }
}
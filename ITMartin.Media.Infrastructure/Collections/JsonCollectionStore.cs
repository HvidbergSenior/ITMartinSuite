using System.Text.Json;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Entities;

namespace ITMartin.Media.Infrastructure.Collections;

public sealed class JsonCollectionStore : ICollectionStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public async Task<List<MediaCollection>> LoadAsync(string libraryPath)
    {
        var filePath = Path.Combine(libraryPath, "collections.json");
        if (!File.Exists(filePath))
            return [];

        var json = await File.ReadAllTextAsync(filePath);

        return JsonSerializer.Deserialize<List<MediaCollection>>(json, Options) ?? [];
    }

    public async Task SaveAsync(string libraryPath, List<MediaCollection> collections)
    {
        var filePath = Path.Combine(libraryPath, "collections.json");
        try
        {
            Directory.CreateDirectory(libraryPath);

            var json = JsonSerializer.Serialize(collections, Options);
            await File.WriteAllTextAsync(filePath, json);
        }
        catch (UnauthorizedAccessException) { }
        catch (IOException) { }
    }
}

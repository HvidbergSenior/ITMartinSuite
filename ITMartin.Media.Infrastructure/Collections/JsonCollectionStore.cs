using System.Text.Json;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Entities;
using Microsoft.Extensions.Configuration;

namespace ITMartin.Media.Infrastructure.Collections;

public sealed class JsonCollectionStore : ICollectionStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    private readonly string _filePath;

    public JsonCollectionStore(IConfiguration configuration)
    {
        var root = configuration["MediaSettings:LibraryRoot"] ?? ".";
        _filePath = Path.Combine(root, "collections.json");
    }

    public async Task<List<MediaCollection>> LoadAsync()
    {
        if (!File.Exists(_filePath))
            return [];

        var json = await File.ReadAllTextAsync(_filePath);

        return JsonSerializer.Deserialize<List<MediaCollection>>(json, Options) ?? [];
    }

    public async Task SaveAsync(List<MediaCollection> collections)
    {
        var dir = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(collections, Options);
        await File.WriteAllTextAsync(_filePath, json);
    }
}

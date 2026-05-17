namespace ITMartin.Media.Infrastructure.Snapshots;

using System.Text.Json;
using ITMartin.Media.Application.Abstractions.Snapshots;

public sealed class JsonScanSnapshotService
    : IScanSnapshotService
{
    private readonly string _snapshotFolder;

    public JsonScanSnapshotService()
    {
        _snapshotFolder =
            Path.Combine(
                AppContext.BaseDirectory,
                "snapshots");

        Directory.CreateDirectory(
            _snapshotFolder);
    }

    public async Task SaveAsync(
        Guid sessionId,
        object state,
        CancellationToken cancellationToken)
    {
        var file =
            GetFilePath(sessionId);

        var json =
            JsonSerializer.Serialize(
                state,
                new JsonSerializerOptions
                {
                    WriteIndented = true
                });

        await File.WriteAllTextAsync(
            file,
            json,
            cancellationToken);
    }

    public async Task<T?> LoadAsync<T>(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var file =
            GetFilePath(sessionId);

        if (!File.Exists(file))
        {
            return default;
        }

        var json =
            await File.ReadAllTextAsync(
                file,
                cancellationToken);

        return JsonSerializer.Deserialize<T>(json);
    }

    private string GetFilePath(
        Guid sessionId)
    {
        return Path.Combine(
            _snapshotFolder,
            $"{sessionId}.json");
    }
}
using System.Text.Json;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;

namespace ITMartin.Media.Infrastructure.Collections;

public sealed class FileStatusRegistryService : IFileStatusRegistryService
{
    private const string FileName = "filestatus.json";

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public async Task<Dictionary<string, FileStatusRecord>> LoadAsync(string libraryPath, CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(libraryPath, FileName);
        if (!File.Exists(path))
            return new Dictionary<string, FileStatusRecord>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var json = await File.ReadAllTextAsync(path, cancellationToken);
            var dict = JsonSerializer.Deserialize<Dictionary<string, FileStatusRecord>>(json);
            return dict is null
                ? new Dictionary<string, FileStatusRecord>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, FileStatusRecord>(dict, StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception)
        {
            // Corrupt/partial sidecar (e.g. killed mid-write) - never let a
            // bad file block re-processing, just start from empty. Every
            // record here is re-derivable by re-checking the actual file.
            return new Dictionary<string, FileStatusRecord>(StringComparer.OrdinalIgnoreCase);
        }
    }

    public async Task SaveAsync(string libraryPath, Dictionary<string, FileStatusRecord> registry, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(libraryPath);
        var path = Path.Combine(libraryPath, FileName);
        var json = JsonSerializer.Serialize(registry, JsonOptions);
        await File.WriteAllTextAsync(path, json, cancellationToken);
    }

    public FileStatusReport BuildReport(Dictionary<string, FileStatusRecord> registry)
    {
        var report = new FileStatusReport { TotalFiles = registry.Count };
        var sampledPerFlag = new Dictionary<string, int>();

        foreach (var record in registry.Values)
        {
            if (record.IsDone) report.DoneFiles++;

            if (!string.IsNullOrWhiteSpace(record.Category))
                report.ByCategory[record.Category] = report.ByCategory.GetValueOrDefault(record.Category) + 1;

            foreach (var flagName in record.ApplicableFlags)
            {
                var isTrue = record.Flags.TryGetValue(flagName, out var state) && state.Value;
                if (isTrue) continue;

                report.OutstandingByFlag[flagName] = report.OutstandingByFlag.GetValueOrDefault(flagName) + 1;

                var sampled = sampledPerFlag.GetValueOrDefault(flagName);
                if (sampled < 10)
                {
                    report.Sample.Add(new OutstandingItem
                    {
                        RelativePath = record.RelativePath,
                        Flag = flagName,
                        Suggestion = record.Flags.TryGetValue(flagName, out var s) ? s.Suggestion : null,
                    });
                    sampledPerFlag[flagName] = sampled + 1;
                }
            }
        }

        return report;
    }
}

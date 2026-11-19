using System.Text.Json;
using ITMartinBudget.Application.Interfaces;
using ITMartinBudget.Domain.Entities;

namespace ITMartinBudget.Infrastructure.Services;

// Ugemål for all ledgers in one small JSON file next to budget.db ({ "bertil": [ {Category, KrPerWeek} ] }).
// A handful of goals per ledger - a table and a migration would be more moving parts than the data.
public sealed class JsonWeeklyGoalStore : IWeeklyGoalStore
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    private readonly string _path;

    public JsonWeeklyGoalStore(string path) => _path = path;

    public async Task<IReadOnlyList<WeeklyGoal>> GetAsync(string ledgerId, CancellationToken ct = default)
    {
        await Gate.WaitAsync(ct);
        try { return (await ReadAsync(ct)).TryGetValue(Key(ledgerId), out var g) ? g : []; }
        finally { Gate.Release(); }
    }

    public async Task SaveAsync(string ledgerId, IReadOnlyList<WeeklyGoal> goals, CancellationToken ct = default)
    {
        await Gate.WaitAsync(ct);
        try
        {
            var all = await ReadAsync(ct);
            all[Key(ledgerId)] = goals.ToList();
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_path))!);
            var tmp = _path + ".tmp";
            await File.WriteAllTextAsync(tmp, JsonSerializer.Serialize(all, Json), ct);
            File.Move(tmp, _path, overwrite: true);
        }
        finally { Gate.Release(); }
    }

    private async Task<Dictionary<string, List<WeeklyGoal>>> ReadAsync(CancellationToken ct)
    {
        if (!File.Exists(_path)) return new();
        var text = await File.ReadAllTextAsync(_path, ct);
        return JsonSerializer.Deserialize<Dictionary<string, List<WeeklyGoal>>>(text) ?? new();
    }

    private static string Key(string ledgerId) => ledgerId.Trim().ToLowerInvariant();
}

using System.Text.Json;
using ITMartinTjek.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace ITMartinTjek.Application;

// ---- ports ----

public interface ISnapshotStore
{
    Task AddAsync(Snapshot s);
    Task<List<string>> DevicesAsync(string ownerKey);
    Task<List<Snapshot>> RunsAsync(string ownerKey, string device, int take);
    Task<Snapshot?> LatestAsync(string ownerKey, string device, string kind);
    Task DeleteAsync(string ownerKey, int id);
}

public interface IAdvisor
{
    bool Enabled { get; }
    Task<Advice> AskAsync(string device, string browserJson, string? deepJson, string description, CancellationToken ct = default);
}

// ---- use cases ----

public sealed record DeepRun(DateTime At, List<Check> Checks, string Json);
public sealed record DeepReceipt(int Ok, int Warn, int Bad, string Message);

/// <summary>Saving runs: from the page, and from the Windows script (POST /api/deep).</summary>
public sealed class Runs(ISnapshotStore store)
{
    public async Task<bool> SaveBrowserRunAsync(string device, string ownerKey, IReadOnlyList<Check> checks)
    {
        if (checks.Count == 0 || string.IsNullOrWhiteSpace(device) || CheckRules.OwnerKey(ownerKey) is not { } key) return false;
        await store.AddAsync(CheckRules.BrowserRun(device, key, checks));
        return true;
    }

    /// <summary>The script's JSON as it came. Throws ArgumentException with the Danish reason when it can not be saved.</summary>
    public async Task<DeepReceipt> ReceiveDeepRunAsync(JsonElement root)
    {
        var device = root.TryGetProperty("device", out var d) ? (d.GetString() ?? "").Trim() : "";
        if (device.Length == 0) throw new ArgumentException("device mangler");
        var key = CheckRules.OwnerKey(root.TryGetProperty("key", out var k) ? k.GetString() : null)
                  ?? throw new ArgumentException("Koden fra tjek-siden mangler. Hent scriptet igen.");
        var statuses = root.TryGetProperty("checks", out var c) && c.ValueKind == JsonValueKind.Array
            ? c.EnumerateArray().Select(x => x.TryGetProperty("status", out var st) ? st.GetString() : null)
            : [];
        var (ok, warn, bad) = CheckRules.Tally(statuses);
        await store.AddAsync(new Snapshot
        {
            Device = device, OwnerKey = key, Kind = SnapshotKind.Deep, Json = root.GetRawText(),
            Ok = ok, Warn = warn, Bad = bad, Summary = CheckRules.Summary(ok, warn, bad),
        });
        return new DeepReceipt(ok, warn, bad, $"Modtaget: {ok} ok, {warn} gule, {bad} røde. Åbn https://pctjek.itmartin.dk og vælg enheden '{device}'.");
    }

    /// <summary>The newest deep-script run for this device in this browser, or null.</summary>
    public async Task<DeepRun?> LatestDeepAsync(string ownerKey, string device)
    {
        if (CheckRules.OwnerKey(ownerKey) is not { } key) return null;
        var s = await store.LatestAsync(key, device.Trim(), SnapshotKind.Deep);
        return s is null ? null : new DeepRun(s.At, CheckRules.Parse(s.Json), s.Json);
    }
}

/// <summary>Historik: only ever this browser's own runs.</summary>
public sealed class History(ISnapshotStore store)
{
    public Task<List<string>> DevicesAsync(string ownerKey) =>
        CheckRules.OwnerKey(ownerKey) is { } key ? store.DevicesAsync(key) : Task.FromResult(new List<string>());

    public Task<List<Snapshot>> RunsAsync(string ownerKey, string device) =>
        CheckRules.OwnerKey(ownerKey) is { } key && device != "" ? store.RunsAsync(key, device, 30) : Task.FromResult(new List<Snapshot>());

    public Task DeleteAsync(string ownerKey, int id) =>
        CheckRules.OwnerKey(ownerKey) is { } key ? store.DeleteAsync(key, id) : Task.CompletedTask;
}

/// <summary>"Hvad er galt?" - one AI call per question, within the day's budget.</summary>
public sealed class Diagnosis(IAdvisor advisor, AiBudget budget)
{
    public bool Enabled => advisor.Enabled;
    public int UsedToday => budget.Used;
    public int MaxPerDay => budget.Max;

    public Task<Advice> AskAsync(string device, IReadOnlyList<Check> checks, string? deepJson, string description)
    {
        if (!advisor.Enabled) throw new InvalidOperationException("AI er ikke slået til");
        budget.Take("ask");
        return advisor.AskAsync(string.IsNullOrWhiteSpace(device) ? "ukendt" : device.Trim(), JsonSerializer.Serialize(checks), deepJson, description);
    }
}

public static class DependencyInjection
{
    public static IServiceCollection AddTjekApplication(this IServiceCollection services, int maxAiCallsPerDay)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(sp => new AiBudget(maxAiCallsPerDay, sp.GetRequiredService<TimeProvider>(),
            sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<AiBudget>>()));
        services.AddSingleton<Runs>();
        services.AddSingleton<History>();
        services.AddSingleton<Diagnosis>();
        return services;
    }
}

using ITMartinBibliotek.Application;
using Microsoft.EntityFrameworkCore;

namespace ITMartinBibliotek.Infrastructure;

// Connection details and API keys are entered once in Indstillinger and
// kept in the database, so a deploy never needs new env vars for them.
public sealed class SettingsStore(IDbContextFactory<BibliotekDbContext> dbFactory) : ISettings
{
    public async Task<string> GetAsync(string key)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return (await db.Settings.FindAsync(key))?.Value ?? string.Empty;
    }

    public async Task<Dictionary<string, string>> GetAllAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Settings.ToDictionaryAsync(s => s.Key, s => s.Value);
    }

    public async Task SetAsync(string key, string value)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var row = await db.Settings.FindAsync(key);
        if (row is null) db.Settings.Add(new Setting { Key = key, Value = value.Trim() });
        else row.Value = value.Trim();
        await db.SaveChangesAsync();
    }
}

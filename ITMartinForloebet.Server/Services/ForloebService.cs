using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ITMartinForloebet.Server.Data;
using ITMartinForloebet.Server.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ITMartinForloebet.Server.Services;

/// <summary>Everything that changes a forløb goes through here, so the analysis and
/// the front-page headline are refreshed in one place, in the background.</summary>
public sealed class ForloebService(
    IDbContextFactory<ForloebetDbContext> factory,
    ForloebetAi ai,
    DocumentStore docs,
    ILogger<ForloebService> logger)
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    // ------------------------------------------------------------------ read

    public async Task<Forloeb?> GetAsync(string slug)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Forloeb.Include(f => f.Trin).Include(f => f.Dokumenter).AsNoTracking()
            .FirstOrDefaultAsync(f => f.Slug == slug);
    }

    public async Task<List<Forloeb>> DelteAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Forloeb.Include(f => f.Trin).AsNoTracking()
            .Where(f => f.Delt)
            .OrderBy(f => f.ClosedAt != null).ThenByDescending(f => f.UpdatedAt)
            .ToListAsync();
    }

    public async Task<string> OverskriftAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        return (await db.SiteState.AsNoTracking().FirstOrDefaultAsync())?.Overskrift ?? "";
    }

    public static Analyse AnalyseAf(Forloeb f)
    {
        if (string.IsNullOrWhiteSpace(f.AnalyseJson)) return new Analyse();
        try { return JsonSerializer.Deserialize<Analyse>(f.AnalyseJson, Json) ?? new Analyse(); }
        catch { return new Analyse(); }
    }

    // ---------------------------------------------------------------- create

    public async Task<Forloeb> OpretAsync(Omskrivning o, DateTime startedAt, bool delt)
    {
        await using var db = await factory.CreateDbContextAsync();
        var omraade = string.IsNullOrWhiteSpace(o.Omraade) ? "Andet" : o.Omraade.Trim();
        var modpart = string.IsNullOrWhiteSpace(o.Modpart) ? "Ukendt modpart" : o.Modpart.Trim();
        var f = new Forloeb
        {
            Slug = Slugs.ForForloeb(omraade, modpart),
            EditKey = Slugs.EditKey(),
            Titel = string.IsNullOrWhiteSpace(o.Titel) ? omraade : o.Titel.Trim(),
            Modpart = modpart,
            Omraade = omraade,
            StartedAt = startedAt,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Delt = delt,
            Trin = [new Trin { At = startedAt, Part = ParsePart(o.Part), Tekst = o.Tekst.Trim(), Citat = o.Citat.Trim(), CreatedAt = DateTime.UtcNow }]
        };
        db.Forloeb.Add(f);
        await db.SaveChangesAsync();
        QueueRefresh(f.Id);
        return f;
    }

    // -------------------------------------------------------------- add step

    public async Task<bool> TilfoejTrinAsync(string slug, string key, DateTime at, Part part, string tekst, string citat)
    {
        await using var db = await factory.CreateDbContextAsync();
        var f = await db.Forloeb.Include(x => x.Trin).FirstOrDefaultAsync(x => x.Slug == slug);
        if (f is null || f.EditKey != key || f.Lukket) return false;
        if (f.Trin.Count >= docs.MaxTrinPerForloeb) return false;
        f.Trin.Add(new Trin { At = at, Part = part, Tekst = tekst.Trim(), Citat = citat.Trim(), CreatedAt = DateTime.UtcNow });
        if (at < f.StartedAt) f.StartedAt = at;
        f.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        QueueRefresh(f.Id);
        return true;
    }

    public async Task<bool> SletTrinAsync(string slug, string key, int trinId)
    {
        await using var db = await factory.CreateDbContextAsync();
        var f = await db.Forloeb.Include(x => x.Trin).FirstOrDefaultAsync(x => x.Slug == slug);
        if (f is null || f.EditKey != key || f.Lukket || f.Trin.Count <= 1) return false;
        var t = f.Trin.FirstOrDefault(x => x.Id == trinId);
        if (t is null) return false;
        f.Trin.Remove(t);
        f.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        QueueRefresh(f.Id);
        return true;
    }

    // -------------------------------------------------------------- documents

    public async Task<Dokument?> GemDokumentAsync(string slug, string key, string fileName, string contentType, long size, Stream data, CancellationToken ct)
    {
        if (size > docs.MaxBytes) return null;
        await using var db = await factory.CreateDbContextAsync();
        var f = await db.Forloeb.Include(x => x.Dokumenter).FirstOrDefaultAsync(x => x.Slug == slug, ct);
        if (f is null || f.EditKey != key || f.Lukket) return null;
        if (f.Dokumenter.Count >= docs.MaxPerForloeb) return null;
        var stored = await docs.SaveAsync(slug, fileName, data, ct);
        var d = new Dokument { FileName = fileName, StoredName = stored, ContentType = contentType, Size = size, CreatedAt = DateTime.UtcNow };
        f.Dokumenter.Add(d);
        f.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return d;
    }

    /// <summary>Let Claude read one stored document and propose timeline steps.</summary>
    public async Task<List<TrinForslag>> ForeslaaTrinAsync(Forloeb f, Dokument d, string? pastedText, CancellationToken ct)
    {
        string? text = pastedText;
        byte[]? image = null; string? mime = null; byte[]? pdf = null;
        if (d.StoredName.Length > 0)
        {
            var path = docs.PathFor(d.StoredName);
            if (DocumentStore.IsImage(d.ContentType)) { image = await DocumentStore.ShrinkForAiAsync(path, ct); mime = "image/jpeg"; }
            else if (DocumentStore.IsPdf(d.ContentType, d.FileName)) pdf = await File.ReadAllBytesAsync(path, ct);
            else if (DocumentStore.IsText(d.ContentType, d.FileName)) text = DocumentStore.ReadTextForAi(path);
            else return [];
        }
        return await ai.TrinFraDokumentAsync(f, text, image, mime, pdf, ct);
    }

    // ------------------------------------------------------------------ close

    public async Task<bool> AfslutAsync(string slug, string key, Udfald udfald, string udfaldTekst, string laering, bool delt)
    {
        await using var db = await factory.CreateDbContextAsync();
        var f = await db.Forloeb.FirstOrDefaultAsync(x => x.Slug == slug);
        if (f is null || f.EditKey != key) return false;
        f.Udfald = udfald;
        f.UdfaldTekst = udfaldTekst.Trim();
        f.Laering = laering.Trim();
        f.Delt = delt;
        f.ClosedAt ??= DateTime.UtcNow;
        f.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        QueueRefresh(f.Id);
        return true;
    }

    public async Task<bool> SaetDeltAsync(string slug, string key, bool delt)
    {
        await using var db = await factory.CreateDbContextAsync();
        var f = await db.Forloeb.FirstOrDefaultAsync(x => x.Slug == slug);
        if (f is null || f.EditKey != key) return false;
        f.Delt = delt;
        f.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        QueueRefresh(f.Id);
        return true;
    }

    // ------------------------------------------------- background refresh

    public event Action<int>? Opdateret;

    /// <summary>One analysis call + (when the shared set changed) one headline call.
    /// Fire-and-forget; the page listens on <see cref="Opdateret"/>.</summary>
    private void QueueRefresh(int forloebId) => _ = Task.Run(async () =>
    {
        try
        {
            await using var db = await factory.CreateDbContextAsync();
            var f = await db.Forloeb.Include(x => x.Trin).FirstOrDefaultAsync(x => x.Id == forloebId);
            if (f is null) return;
            var delteLukkede = await db.Forloeb.AsNoTracking().Where(x => x.Delt && x.ClosedAt != null).ToListAsync();
            var a = await ai.AnalyserAsync(f, delteLukkede);
            f.AnalyseJson = JsonSerializer.Serialize(a);
            f.Resume = a.Resume;
            await db.SaveChangesAsync();
            await RefreshOverskriftAsync(db);
        }
        catch (AiBudgetExceededException) { logger.LogWarning("Refresh skipped, AI budget spent"); }
        catch (Exception ex) { logger.LogError(ex, "Refresh failed for forløb {Id}", forloebId); }
        finally { Opdateret?.Invoke(forloebId); }
    });

    private async Task RefreshOverskriftAsync(ForloebetDbContext db)
    {
        var delte = await db.Forloeb.Include(x => x.Trin).AsNoTracking().Where(x => x.Delt).ToListAsync();
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            string.Join("|", delte.Select(x => $"{x.Id}:{x.Udfald}:{x.ClosedAt:d}:{x.Titel}")))));
        var state = await db.SiteState.FirstOrDefaultAsync() ?? db.SiteState.Add(new SiteState()).Entity;
        if (state.OverskriftHash == hash) return;
        state.Overskrift = delte.Count == 0 ? "" : await ai.OverskriftAsync(delte);
        state.OverskriftHash = hash;
        state.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    public static Part ParsePart(string s) => s.Trim().ToLowerInvariant() switch
    {
        "modpart" => Part.Modpart,
        "anden" => Part.Anden,
        _ => Part.Borger
    };
}

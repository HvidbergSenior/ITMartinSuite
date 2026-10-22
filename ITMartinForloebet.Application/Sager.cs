using ITMartinForloebet.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ITMartinForloebet.Application;

// ---- ports ----

public interface IForloebStore
{
    /// <summary>The whole forløb (steps + documents), read-only.</summary>
    Task<Forloeb?> GetAsync(string slug);
    Task<Forloeb?> GetByIdAsync(int id);
    Task<List<Forloeb>> DelteAsync();
    Task<List<Forloeb>> DelteLukkedeAsync();
    Task AddAsync(Forloeb f);
    /// <summary>Loads the forløb (steps + documents), lets <paramref name="change"/> apply a domain rule and saves when it returns true.</summary>
    Task<bool> ChangeAsync(string slug, Func<Forloeb, bool> change, CancellationToken ct = default);
    Task<bool> ChangeAsync(int id, Func<Forloeb, bool> change);
    Task<SiteState> SiteStateAsync();
    Task SaveSiteStateAsync(SiteState s);
}

/// <summary>A document as the AI gets it: text, a shrunk photo, or a PDF.</summary>
public sealed record DokumentIndhold(string? Text, byte[]? Image, string? ImageMime, byte[]? Pdf);

public interface IDocumentFiles
{
    Task<string> SaveAsync(string slug, string fileName, Stream data, CancellationToken ct);
    string PathFor(string storedName);
    /// <summary>Null when the AI cannot read this kind of file.</summary>
    Task<DokumentIndhold?> ReadForAiAsync(Dokument d, CancellationToken ct);
}

/// <summary>Claude. The day budget is taken by <see cref="Sager"/> before each call.</summary>
public interface IForloebAi
{
    Task<Omskrivning> OmskrivAsync(string raw, Forloeb? existing, CancellationToken ct = default);
    Task<List<TrinForslag>> TrinFraDokumentAsync(Forloeb f, DokumentIndhold indhold, CancellationToken ct = default);
    Task<Analyse> AnalyserAsync(Forloeb f, IReadOnlyList<Forloeb> delteAfsluttede, CancellationToken ct = default);
    Task<List<string>> ForeslaaLaeringAsync(Forloeb f, Udfald udfald, string udfaldTekst, CancellationToken ct = default);
    Task<string> OverskriftAsync(IReadOnlyList<Forloeb> delte, CancellationToken ct = default);
}

// ---- use cases ----

/// <summary>Everything that changes a forløb goes through here, so the analysis and
/// the front-page headline are refreshed in one place, in the background.</summary>
public sealed class Sager(IForloebStore store, IDocumentFiles files, IForloebAi ai, AiBudget budget,
    ForloebLimits limits, TimeProvider clock, ILogger<Sager> logger)
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    // ------------------------------------------------------------------ read

    public Task<Forloeb?> GetAsync(string slug) => store.GetAsync(slug);
    public Task<List<Forloeb>> DelteAsync() => store.DelteAsync();
    public async Task<string> OverskriftAsync() => (await store.SiteStateAsync()).Overskrift;

    /// <summary>The other shared stories with the same counterpart.</summary>
    public async Task<List<Forloeb>> LignendeAsync(Forloeb f)
    {
        var key = ForloebRules.ModpartNoegle(f.Modpart);
        return (await store.DelteAsync()).Where(x => x.Id != f.Id && ForloebRules.ModpartNoegle(x.Modpart) == key).ToList();
    }

    /// <summary>Counterparts that already have shared stories, by their most used spelling - offered when a new story starts.</summary>
    public async Task<List<string>> ModparterAsync() =>
        (await store.DelteAsync()).GroupBy(x => ForloebRules.ModpartNoegle(x.Modpart))
            .Select(g => g.GroupBy(x => x.Modpart).OrderByDescending(n => n.Count()).First().Key)
            .OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase).ToList();

    // -------------------------------------------------------------- AI help

    /// <summary>The user's informal message rewritten for publishing (nothing is saved yet).</summary>
    public Task<Omskrivning> OmskrivAsync(string raw, Forloeb? existing)
    {
        budget.Take("omskriv");
        return ai.OmskrivAsync(raw, existing);
    }

    public Task<List<string>> ForeslaaLaeringAsync(Forloeb f, Udfald udfald, string udfaldTekst)
    {
        budget.Take("laering");
        return ai.ForeslaaLaeringAsync(f, udfald, udfaldTekst);
    }

    /// <summary>Claude reads one stored document (or pasted text when <paramref name="d"/> is null) and proposes timeline steps.</summary>
    public async Task<List<TrinForslag>> ForeslaaTrinAsync(Forloeb f, Dokument? d, string? pastedText, CancellationToken ct)
    {
        var indhold = d is null ? new DokumentIndhold(pastedText, null, null, null) : await files.ReadForAiAsync(d, ct);
        if (indhold is null) return [];
        budget.Take("dokument");
        return await ai.TrinFraDokumentAsync(f, indhold, ct);
    }

    // ---------------------------------------------------------------- change

    public async Task<Forloeb> OpretAsync(Omskrivning o, DateTime startedAt, bool delt)
    {
        // A counterpart that already has stories keeps its spelling, so the new story joins that group.
        var key = ForloebRules.ModpartNoegle(o.Modpart);
        if (key.Length > 0 && (await ModparterAsync()).FirstOrDefault(m => ForloebRules.ModpartNoegle(m) == key) is { } kendt)
            o.Modpart = kendt;
        var f = Forloeb.Opret(o, startedAt, delt, Now);
        await store.AddAsync(f);
        QueueRefresh(f.Id);
        return f;
    }

    public Task<bool> TilfoejTrinAsync(string slug, string key, DateTime at, Part part, string tekst, string citat) =>
        ChangedAsync(slug, f => f.TilfoejTrin(key, at, part, tekst, citat, limits, Now));

    /// <summary>The steps the owner ticked among the AI's suggestions.</summary>
    public async Task TilfoejForslagAsync(string slug, string key, IEnumerable<TrinForslag> valgte)
    {
        foreach (var p in valgte)
            await store.ChangeAsync(slug, f => f.TilfoejTrin(key, ForloebRules.DatoEllerNu(p.Dato, Now), ForloebRules.ParsePart(p.Part), p.Tekst, p.Citat, limits, Now));
        if (await store.GetAsync(slug) is { } after) QueueRefresh(after.Id);
    }

    public Task<bool> SletTrinAsync(string slug, string key, int trinId) =>
        ChangedAsync(slug, f => f.SletTrin(key, trinId, Now));

    public Task<bool> AfslutAsync(string slug, string key, Udfald udfald, string udfaldTekst, string laering, bool delt) =>
        ChangedAsync(slug, f => f.Afslut(key, udfald, udfaldTekst, laering, delt, Now));

    /// <summary>No AI refresh: the why and the prediction are the owner's own words, not part of the analysis.</summary>
    public Task<bool> SaetHvorforAsync(string slug, string key, string hvorfor, string forudsigelse) =>
        store.ChangeAsync(slug, f => f.SaetHvorfor(key, hvorfor, forudsigelse, Now));

    public Task<bool> SaetDeltAsync(string slug, string key, bool delt) =>
        ChangedAsync(slug, f => f.SaetDelt(key, delt, Now));

    /// <summary>Null when the file is too big, there are too many, the key is wrong or the forløb is closed.</summary>
    public async Task<Dokument?> GemDokumentAsync(string slug, string key, string fileName, string contentType, long size, Stream data, CancellationToken ct)
    {
        var f = await store.GetAsync(slug);
        if (f is null || !f.KanTilfoejeDokument(key, size, limits)) return null;
        var stored = await files.SaveAsync(slug, fileName, data, ct);
        Dokument? d = null;
        var ok = await store.ChangeAsync(slug, x =>
        {
            if (!x.KanTilfoejeDokument(key, size, limits)) return false;
            d = x.TilfoejDokument(fileName, stored, contentType, size, Now);
            return true;
        }, ct);
        return ok ? d : null;
    }

    /// <summary>Owner-only evidence: the file is only handed out with the forløb's key.</summary>
    public async Task<(string Path, Dokument Dokument)?> DokumentFilAsync(string slug, int id, string? key)
    {
        var f = await store.GetAsync(slug);
        if (f is null || !f.HarNoegle(key)) return null;
        var d = f.Dokumenter.FirstOrDefault(x => x.Id == id);
        return d is null ? null : (files.PathFor(d.StoredName), d);
    }

    // ------------------------------------------------- background refresh

    public event Action<int>? Opdateret;

    private async Task<bool> ChangedAsync(string slug, Func<Forloeb, bool> change)
    {
        var id = 0;
        var ok = await store.ChangeAsync(slug, f => { id = f.Id; return change(f); });
        if (ok) QueueRefresh(id);
        return ok;
    }

    /// <summary>One analysis call + (when the shared set changed) one headline call.
    /// Fire-and-forget; the page listens on <see cref="Opdateret"/>.</summary>
    private void QueueRefresh(int forloebId) => _ = Task.Run(() => RefreshAsync(forloebId));

    internal async Task RefreshAsync(int forloebId)
    {
        try
        {
            var f = await store.GetByIdAsync(forloebId);
            if (f is null) return;
            var delteLukkede = await store.DelteLukkedeAsync();
            budget.Take("analyse");
            var a = await ai.AnalyserAsync(f, delteLukkede);
            await store.ChangeAsync(forloebId, x => { x.GemAnalyse(a); return true; });
            await RefreshOverskriftAsync();
        }
        catch (AiBudgetExceededException) { logger.LogWarning("Refresh skipped, AI budget spent"); }
        catch (Exception ex) { logger.LogError(ex, "Refresh failed for forløb {Id}", forloebId); }
        finally { Opdateret?.Invoke(forloebId); }
    }

    private async Task RefreshOverskriftAsync()
    {
        var delte = await store.DelteAsync();
        var hash = ForloebRules.OverskriftHash(delte);
        var state = await store.SiteStateAsync();
        if (state.OverskriftHash == hash) return;
        if (delte.Count > 0) budget.Take("overskrift");
        state.Overskrift = delte.Count == 0 ? "" : await ai.OverskriftAsync(delte);
        state.OverskriftHash = hash;
        state.UpdatedAt = Now;
        await store.SaveSiteStateAsync(state);
    }
}

public static class DependencyInjection
{
    public static IServiceCollection AddForloebetApplication(this IServiceCollection services, ForloebLimits limits, int maxAiCallsPerDay)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(limits);
        services.AddSingleton(sp => new AiBudget(maxAiCallsPerDay, sp.GetRequiredService<TimeProvider>(), sp.GetRequiredService<ILogger<AiBudget>>()));
        services.AddSingleton<Sager>();
        return services;
    }
}

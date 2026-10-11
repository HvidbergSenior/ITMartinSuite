using ITMartinLager.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace ITMartinLager.Server.Services;

// Kolofon = the back of the title page: edition, printing, year, publisher, ISBN (2026-10-11, user: "to save in db and
// get price just cover. A job can run through kolofon if needed"). The cover photo stays the only step for every item;
// a kolofon photo is asked for only when the prices say the edition matters - see Needed.
public static class Kolofon
{
    // A used copy somewhere at this price or more = it may be a first edition or a sought-after printing (Lager__KolofonFromKr).
    public static int FromKr(IConfiguration cfg) => int.TryParse(cfg["Lager:KolofonFromKr"], out var kr) ? kr : 150;

    // Books still waiting for a kolofon photo, highest price found first.
    public static async Task<List<(Item Item, int TopKr)>> NeededAsync(LagerDb db, int fromKr, int take = 200)
    {
        var top = await db.PriceFinds.Where(f => !f.IsNew)
            .GroupBy(f => f.ItemId).Select(g => new { ItemId = g.Key, Top = g.Max(f => f.Kr) })
            .Where(x => x.Top >= fromKr).ToListAsync();
        var byId = top.ToDictionary(x => x.ItemId, x => x.Top);
        var ids = byId.Keys.ToList();
        var items = await db.Items.Include(i => i.Box)
            .Where(i => ids.Contains(i.Id) && i.Kind == Kinds.Bog && i.KolofonPhoto == "" && i.Status != Statuses.Solgt)
            .ToListAsync();
        return items.Select(i => (i, byId[i.Id])).OrderByDescending(x => x.Item2).Take(take).ToList();
    }

    public static async Task SavePhotoAsync(LagerDb db, string dataDir, Item item, Stream photo)
    {
        var jpeg = await PileReader.ShrinkAsync(photo, CancellationToken.None);
        var rel = Path.Combine("photos", "kolofon", $"{item.Id}.jpg");
        var full = Path.Combine(dataDir, rel);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        await File.WriteAllBytesAsync(full, jpeg);
        item.KolofonPhoto = rel;
        item.KolofonAt = null;   // the reader takes it next
        item.Touch();
        await db.SaveChangesAsync();
    }

    // What the AI read goes onto the item. A new ISBN = the price hunter searches again, now for exactly that edition.
    public static void Apply(Item item, PileReader.KolofonRow r)
    {
        var isbn = BarcodeLookup.ToIsbn13(new string((r.Isbn ?? "").Where(c => char.IsDigit(c) || c is 'X' or 'x').ToArray()));
        if (BarcodeLookup.IsIsbn(isbn) && isbn != item.Barcode) { item.Barcode = isbn; item.PricesAt = null; }
        if (!string.IsNullOrWhiteSpace(r.Edition)) item.Edition = r.Edition.Trim();
        if (!string.IsNullOrWhiteSpace(r.Publisher)) item.Publisher = r.Publisher.Trim();
        if (r.Year is > 1450 and < 2100) item.Year = r.Year;
        if (!string.IsNullOrWhiteSpace(r.Note) && !item.Note.Contains(r.Note.Trim()))
            item.Note = (item.Note + " Kolofon: " + r.Note.Trim()).Trim();
        item.KolofonAt = DateTime.UtcNow;
        item.Touch();
    }
}

// Reads waiting kolofon photos, up to 10 in one AI call, every 2 minutes. Lager__KolofonReader=false turns it off.
public sealed class KolofonReader(IDbContextFactory<LagerDb> dbf, PileReader reader, IConfiguration cfg, ILogger<KolofonReader> log)
    : BackgroundService
{
    public const int PerCall = 10;

    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        if (!cfg.GetValue("Lager:KolofonReader", true) || !reader.Enabled) { log.LogInformation("Kolofon reader is off"); return; }
        var dataDir = Program.DataDir(cfg);
        while (!stop.IsCancellationRequested)
        {
            try
            {
                await using var db = await dbf.CreateDbContextAsync(stop);
                var waiting = await db.Items.Where(i => i.KolofonPhoto != "" && i.KolofonAt == null)
                    .OrderBy(i => i.UpdatedAt).Take(PerCall).ToListAsync(stop);
                var withFile = waiting.Where(i => File.Exists(Path.Combine(dataDir, i.KolofonPhoto))).ToList();
                foreach (var gone in waiting.Except(withFile)) { gone.KolofonPhoto = ""; gone.Touch(); }
                if (withFile.Count > 0)
                {
                    var jpegs = new List<byte[]>();
                    foreach (var i in withFile) jpegs.Add(await File.ReadAllBytesAsync(Path.Combine(dataDir, i.KolofonPhoto), stop));
                    var rows = await reader.ReadKolofonsAsync(jpegs, stop);
                    for (var n = 0; n < withFile.Count; n++)
                    {
                        if (rows.TryGetValue(n, out var r)) Kolofon.Apply(withFile[n], r);
                        else { withFile[n].KolofonAt = DateTime.UtcNow; withFile[n].Note = (withFile[n].Note + " Kolofon kunne ikke læses.").Trim(); withFile[n].Touch(); }
                    }
                }
                await db.SaveChangesAsync(stop);
            }
            catch (OperationCanceledException) { return; }
            catch (Exception ex) { log.LogWarning("Kolofon round failed: {Message}", ex.Message); }   // cap reached etc. - try again later
            try { await Task.Delay(TimeSpan.FromMinutes(2), stop); } catch (OperationCanceledException) { return; }
        }
    }
}

using ITMartinLager.Application;
using ITMartinVarer.Contracts;

namespace ITMartinLager.Server;

// The API Vurdering (vurdering-web) calls (2026-11-05): the piles to price, one thing's photo, and the person's price.
// Only with VurderingHeaders.Key = Lager__VurderingKey - the check is in Program.cs. Each endpoint calls a use case.
public static class VurderingApi
{
    public static void MapVurderingApi(this WebApplication app)
    {
        var api = app.MapGroup("/api/vurdering");

        api.MapGet("/koe", async (string? bunke, int? antal, string? spring, string? brug, PriceReviewUseCases review) =>
        {
            var skip = (spring ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries).Select(x => int.TryParse(x, out var n) ? n : 0).Where(n => n > 0).ToList();
            return await review.QueueAsync(VurderingPiles.Valid(bunke) ? bunke! : VurderingPiles.Assess, antal ?? 1, skip, default, brug);
        });

        api.MapPost("/varer/{id:int}/brug", async (int id, VurderingUsage body, PriceReviewUseCases review) =>
            await review.SetUsageAsync(id, body.Usage) is { } error ? Results.BadRequest(error) : Results.Ok());

        api.MapPost("/varer/{id:int}/pris", async (int id, VurderingPrice body, PriceReviewUseCases review) =>
            await review.SetAsync(id, body.Kr) is { } error ? Results.BadRequest(error) : Results.Ok());

        // "Find priser nu" moved here from Lager's Vare page (2026-11-15: "pricing should be in pricing"): asks every site again.
        api.MapPost("/varer/{id:int}/find", async (int id, PriceHuntUseCases hunt, CancellationToken ct) =>
            Results.Ok(new VurderingHunted(await hunt.HuntAsync(id, ct))));

        // Guld/sølv/bronze and Ny i kiosken moved to Vurdering (2026-11-15: "pricing should be in pricing"); the data stays here.
        api.MapGet("/guld", async (GoldUseCases gold) => (await gold.AllAsync())
            .Select(e => new VurderingGold(e.Tier, e.Badge, ProductTypes.For(e.Kind), e.Category, e.Issue, e.Name, e.Kr, e.Listings, e.Source, e.Url, e.SeenAt))
            .OrderByDescending(e => e.Kr).ToList());
        api.MapGet("/kiosk", async (KioskUseCases kiosk) => (await kiosk.ListAsync())
            .Select(p => new VurderingKiosk(p.Id, p.Series, p.Kr, p.Note, p.UpdatedAt)).OrderBy(p => p.Series).ToList());
        api.MapPost("/kiosk", async (VurderingKioskSave body, KioskUseCases kiosk) =>
        {
            try { await kiosk.SaveAsync(body.Series ?? "", body.Kr, body.Note ?? ""); return Results.Ok(); }
            catch (LagerException ex) { return Results.BadRequest(ex.Message); }
        });
        api.MapDelete("/kiosk/{id:int}", async (int id, KioskUseCases kiosk) => { await kiosk.DeleteAsync(id); return Results.Ok(); });

        // Descriptions for Ting/Strik (2026-11-10): what waits, and what the AI saw.
        api.MapGet("/beskriv", async (int? antal, ThingDescriptionUseCases things) => await things.WaitingAsync(antal ?? ThingDescriptionUseCases.MaxPerAsk));
        api.MapPost("/varer/{id:int}/beskrivelse", async (int id, VurderingDescription body, ThingDescriptionUseCases things) =>
            await things.KeepAsync(id, body) ? Results.Ok() : Results.NotFound());

        api.MapGet("/varer/{id:int}/foto", async (int id, ItemUseCases items, StockForSaleUseCases stock) =>
            await items.PhotoFileAsync(id) is { } file ? Results.File(file, "image/jpeg")
            : await stock.PilePhotoAsync(id) is { } pile ? Results.File(pile, "image/jpeg") : Results.NotFound());
    }
}

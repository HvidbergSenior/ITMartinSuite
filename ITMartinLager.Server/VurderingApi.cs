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

        // Søg (2026-11-16): one thing by number or words, instead of clicking through the pile.
        api.MapGet("/soeg", async (string? q, string? spring, PriceReviewUseCases review) =>
        {
            var skip = (spring ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries).Select(x => int.TryParse(x, out var n) ? n : 0).Where(n => n > 0).ToList();
            return await review.SearchAsync(q ?? "", skip);
        });

        api.MapPost("/varer/{id:int}/brug", async (int id, VurderingUsage body, PriceReviewUseCases review) =>
            await review.SetUsageAsync(id, body.Usage) is { } error ? Results.BadRequest(error) : Results.Ok());

        api.MapPost("/varer/{id:int}/pris", async (int id, VurderingPrice body, PriceReviewUseCases review) =>
            await review.SetAsync(id, body.Kr) is { } error ? Results.BadRequest(error) : Results.Ok());

        // Sjælden - vurderes manuelt (2026-11-16): mark/unmark one, mark many, and the person's comment.
        api.MapPost("/varer/{id:int}/sjaelden", async (int id, VurderingRare body, PriceReviewUseCases review) =>
            await review.SetRareAsync(id, body.Rare) is { } error ? Results.BadRequest(error) : Results.Ok());
        api.MapPost("/sjaelden", async (VurderingRareIds body, PriceReviewUseCases review) =>
            Results.Ok(new { marked = await review.MarkRareAsync(body.Ids ?? []) }));
        api.MapPost("/varer/{id:int}/note", async (int id, VurderingNote body, PriceReviewUseCases review) =>
            await review.SetNoteAsync(id, body.Note) is { } error ? Results.BadRequest(error) : Results.Ok());

        // The photo (2026-11-16): hide a bad one from bogshoppen.dk, paint over a price mark, or bring the original back.
        api.MapPost("/varer/{id:int}/skjulfoto", async (int id, VurderingPhotoHidden body, ItemUseCases items) =>
            await items.SetPhotoHiddenAsync(id, body.Hidden) is { } error ? Results.BadRequest(error) : Results.Ok());
        api.MapPost("/varer/{id:int}/maerke", async (int id, PhotoArea body, ItemUseCases items) =>
            PhotoArea.From([body.Left, body.Top, body.Right, body.Bottom], 0.005) is not { } area ? Results.BadRequest("Træk en firkant hen over mærket.")
            : await items.CoverMarkAsync(id, area) is { } error ? Results.BadRequest(error) : Results.Ok());
        api.MapPost("/varer/{id:int}/maerke/fortryd", async (int id, ItemUseCases items) =>
            await items.UncoverAsync(id) is { } error ? Results.BadRequest(error) : Results.Ok());

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

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

        api.MapGet("/koe", async (string? bunke, int? antal, string? spring, PriceReviewUseCases review) =>
        {
            var skip = (spring ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries).Select(x => int.TryParse(x, out var n) ? n : 0).Where(n => n > 0).ToList();
            return await review.QueueAsync(VurderingPiles.Valid(bunke) ? bunke! : VurderingPiles.Assess, antal ?? 1, skip);
        });

        api.MapPost("/varer/{id:int}/pris", async (int id, VurderingPrice body, PriceReviewUseCases review) =>
            await review.SetAsync(id, body.Kr) is { } error ? Results.BadRequest(error) : Results.Ok());

        api.MapGet("/varer/{id:int}/foto", async (int id, ItemUseCases items, StockForSaleUseCases stock) =>
            await items.PhotoFileAsync(id) is { } file ? Results.File(file, "image/jpeg")
            : await stock.PilePhotoAsync(id) is { } pile ? Results.File(pile, "image/jpeg") : Results.NotFound());
    }
}

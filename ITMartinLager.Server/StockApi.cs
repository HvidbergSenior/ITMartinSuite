using ITMartinLager.Application;

namespace ITMartinLager.Server;

// The API Salg (salg-web) calls (2026-10-21 split; the shop itself moved there): what is for sale, one thing with its
// proof, its photo, the front page's chosen things, and a paid sale to take out of the stock. Only with the key
// (StockHeaders.Key = Lager__SalgKey) - the check is in Program.cs. The answers are the shapes in ITMartinVarer.Contracts.
// Each endpoint calls a use case.
public static class StockApi
{
    public static void MapStockApi(this WebApplication app)
    {
        var api = app.MapGroup("/api/salg");

        api.MapGet("/varer", async (string[]? art, string? brugt, string? q, string? sort, int? fra, int? antal, StockForSaleUseCases stock) =>
            await stock.ListedAsync(art, brugt, q, sort, fra ?? 0, antal ?? 60));

        api.MapGet("/arter", async (StockForSaleUseCases stock) => await stock.PerKindAsync());

        api.MapGet("/varer/{id:int}", async (int id, StockForSaleUseCases stock) =>
            await stock.ItemAsync(id) is { } page ? Results.Ok(page) : Results.NotFound());

        api.MapPost("/varer", async (StockIds body, StockForSaleUseCases stock) => await stock.ByIdsAsync(body.Ids ?? []));

        api.MapGet("/udvalgte", async (int? antal, StockForSaleUseCases stock) => await stock.FeaturedAsync(antal ?? 12));

        api.MapGet("/varer/{id:int}/foto", async (int id, StockForSaleUseCases stock, ItemUseCases items) =>
            await stock.PhotoPathAsync(id) is not null && await items.PhotoFileAsync(id) is { } file ? Results.File(file, "image/jpeg") : Results.NotFound());

        api.MapPost("/solgt", async (StockSale sale, StockForSaleUseCases stock) =>
        {
            try { return Results.Ok(new { Booked = await stock.BookAsync(sale) }); }
            catch (LagerException ex) { return Results.BadRequest(ex.Message); }
        });
    }
}

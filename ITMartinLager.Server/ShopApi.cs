using ITMartinLager.Application;

namespace ITMartinLager.Server;

// The API bogshoppen.dk (hjem-web) calls for the shop (2026-10-15): search, one item with its proof links, its photo,
// Køb nu and the buyer's return. Only with the shop key (ShopHeaders.Key = Lager__ShopKey) - the check is in Program.cs.
// The answers are the shapes in ITMartinVarer.Contracts - never anything a buyer should not see.
public static class ShopApi
{
    public static void MapShopApi(this WebApplication app)
    {
        var api = app.MapGroup("/api/butik");

        api.MapGet("/soeg", async (string? q, string? art, ShopUseCases shop) =>
            new ShopSearch(shop.CanPay, await shop.SearchAsync(q, art)));

        api.MapGet("/forside", async (ShopUseCases shop) =>
            new ShopFront(shop.CanPay, (await shop.FeaturedAsync()).Select(x => new ShopFeatured(x.Item, x.Proof)).ToList()));

        api.MapGet("/varer/{id:int}", async (int id, ShopUseCases shop) =>
            await shop.ItemAsync(id) is { } x ? Results.Ok(new ShopItemPage(shop.CanPay, x.Item, x.Proof)) : Results.NotFound());

        api.MapGet("/varer/{id:int}/foto", async (int id, ShopUseCases shop, ItemUseCases items) =>
            await shop.PhotoPathAsync(id) is not null && await items.PhotoFileAsync(id) is { } file ? Results.File(file, "image/jpeg") : Results.NotFound());

        api.MapPost("/varer/{id:int}/koeb", async (int id, ShopBuy body, ShopUseCases shop) =>
        {
            if (!Uri.TryCreate(body.SiteBase, UriKind.Absolute, out var site) || site.Scheme is not ("https" or "http"))
                return Results.BadRequest(new ShopBuyAnswer(null, "Ugyldig side."));
            try { return Results.Ok(new ShopBuyAnswer(await shop.BuyAsync(id, site.GetLeftPart(UriPartial.Authority)), null)); }
            catch (LagerException ex) { return Results.BadRequest(new ShopBuyAnswer(null, ex.Message)); }
        });

        api.MapGet("/ordrer/{token}", async (string token, ShopUseCases shop) =>
        {
            try
            {
                return await shop.ConfirmAsync(token) is { } o
                    ? Results.Ok(new ShopOrder(o.Status, o.Line, o.Kr, o.PaidKr, o.Delivery, o.Email))
                    : Results.NotFound();
            }
            catch (LagerException ex) { return Results.Problem(ex.Message, statusCode: 503); }
        });
    }
}

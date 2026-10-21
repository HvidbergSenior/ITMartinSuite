using ITMartinSalg.Application;

namespace ITMartinSalg.Server;

// The API bogshoppen.dk (hjem-web) calls for the shop (2026-10-15 in Lager, moved to Salg 2026-10-21 with the same
// paths and shapes): the marketplace by product type, one item with its proof links, its photo, the basket priced with
// today's offers, Køb nu and the buyer's return. Only with the shop key (ShopHeaders.Key = Salg__ShopKey) - the check
// is in Program.cs. The answers are the
// shapes in ITMartinVarer.Contracts - never anything a buyer should not see. Each endpoint calls a use case.
public static class ShopApi
{
    public sealed record BasketIds(List<int> ItemIds);

    public static void MapShopApi(this WebApplication app)
    {
        var api = app.MapGroup("/api/butik");

        api.MapGet("/soeg", async (string? q, string? art, ShopUseCases shop) =>
            new ShopSearch(shop.CanPay, await shop.SearchAsync(q, art)));

        api.MapGet("/marked", async (string? type, string? art, string? brugt, string? q, string? sort, int? side, ShopUseCases shop) =>
            await shop.MarketAsync(type, art, brugt, q, sort, side ?? 0));

        api.MapGet("/forside", async (ShopUseCases shop) =>
            new ShopFront(shop.CanPay, (await shop.FeaturedAsync()).Select(x => new ShopFeatured(x.Item, x.Proof)).ToList()));

        api.MapGet("/varer/{id:int}", async (int id, ShopUseCases shop) =>
            await shop.ItemAsync(id) is { } x ? Results.Ok(new ShopItemPage(shop.CanPay, x.Item, x.Proof)) : Results.NotFound());

        api.MapGet("/varer/{id:int}/foto", async (int id, ShopUseCases shop) =>
            await shop.PhotoAsync(id) is { } jpeg ? Results.File(jpeg, "image/jpeg") : Results.NotFound());

        api.MapPost("/kurv", async (BasketIds body, ShopUseCases shop) => await shop.QuoteAsync(body.ItemIds ?? []));

        api.MapPost("/varer/{id:int}/koeb", (int id, ShopBuy body, ShopUseCases shop) => Buy(body.ItemIds is { Count: > 0 } ids ? ids : [id], body, shop));
        api.MapPost("/kurv/koeb", (ShopBuy body, ShopUseCases shop) => Buy(body.ItemIds ?? [], body, shop));

        api.MapGet("/ordrer/{token}", async (string token, ShopUseCases shop) =>
        {
            try
            {
                return await shop.ConfirmAsync(token) is { } o
                    ? Results.Ok(new ShopOrder(o.Status, o.Line, o.Kr, o.PaidKr, o.Delivery, o.Email, o.Tracking))
                    : Results.NotFound();
            }
            catch (SalgException ex) { return Results.Problem(ex.Message, statusCode: 503); }
        });
    }

    private static async Task<IResult> Buy(List<int> ids, ShopBuy body, ShopUseCases shop)
    {
        if (!Uri.TryCreate(body.SiteBase, UriKind.Absolute, out var site) || site.Scheme is not ("https" or "http"))
            return Results.BadRequest(new ShopBuyAnswer(null, "Ugyldig side."));
        try { return Results.Ok(new ShopBuyAnswer(await shop.BuyAsync(ids, site.GetLeftPart(UriPartial.Authority)), null)); }
        catch (SalgException ex) { return Results.BadRequest(new ShopBuyAnswer(null, ex.Message)); }
    }
}

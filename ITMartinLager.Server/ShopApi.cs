using ITMartinLager.Application;

namespace ITMartinLager.Server;

// The API bogshoppen.dk (hjem-web) calls for the shop (2026-10-15): search, one item with its proof links, its photo,
// Køb nu and the buyer's return. Only with the shop key (X-Shop-Key = Lager__ShopKey) - the check is in Program.cs.
// Never anything a buyer should not see: no boxes, notes, buyers or purchase prices.
public static class ShopApi
{
    public sealed record BuyBody(string SiteBase);

    public static void MapShopApi(this WebApplication app)
    {
        var api = app.MapGroup("/api/butik");

        api.MapGet("/soeg", async (string? q, string? art, ShopUseCases shop) =>
            new { shop.CanPay, Items = await shop.SearchAsync(q, art) });

        api.MapGet("/varer/{id:int}", async (int id, ShopUseCases shop) =>
            await shop.ItemAsync(id) is { } x ? Results.Ok(new { shop.CanPay, x.Item, x.Proof }) : Results.NotFound());

        api.MapGet("/varer/{id:int}/foto", async (int id, ShopUseCases shop, IPhotoStore photos) =>
            await shop.PhotoPathAsync(id) is { } path && photos.FullPath(path) is { } file ? Results.File(file, "image/jpeg") : Results.NotFound());

        api.MapPost("/varer/{id:int}/koeb", async (int id, BuyBody body, ShopUseCases shop) =>
        {
            if (!Uri.TryCreate(body.SiteBase, UriKind.Absolute, out var site) || site.Scheme is not ("https" or "http"))
                return Results.BadRequest(new { Error = "Ugyldig side." });
            try { return Results.Ok(new { Url = await shop.BuyAsync(id, site.GetLeftPart(UriPartial.Authority)) }); }
            catch (LagerException ex) { return Results.BadRequest(new { Error = ex.Message }); }
        });

        api.MapGet("/ordrer/{token}", async (string token, ShopUseCases shop) =>
        {
            try
            {
                return await shop.ConfirmAsync(token) is { } o
                    ? Results.Ok(new { o.Status, o.Line, o.Kr, o.PaidKr, o.Delivery, o.Email })
                    : Results.NotFound();
            }
            catch (LagerException ex) { return Results.Problem(ex.Message, statusCode: 503); }
        });
    }
}

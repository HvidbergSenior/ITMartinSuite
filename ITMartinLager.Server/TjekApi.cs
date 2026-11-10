using ITMartinLager.Application;
using ITMartinLager.Domain;
using ITMartinVarer.Contracts;

namespace ITMartinLager.Server;

// Vareinfo's Tjek page (2026-11-10, user: "Take picture -> get quick price and/or if something is interesting"): the answers for
// one Tjek session's box and Tog den / Nej tak / Fortryd. Only with CaptureHeaders.Key = Lager__InfoKey (the check is in
// Program.cs) - the same key as Lager's calls to Vareinfo. Each endpoint calls FundUseCases; a thing must be in that box.
public static class TjekApi
{
    public static void MapTjekApi(this WebApplication app)
    {
        var api = app.MapGroup("/api/vareinfo/tjek/{boxCode}");

        api.MapGet("", async (string boxCode, FundUseCases fund) =>
            await fund.TripByCodeAsync(boxCode) is { } t
                ? Results.Ok(new CheckAnswers(t.Open.Select(Answer).ToList(), t.Taken.Select(Answer).ToList(), t.NotBought))
                : Results.Ok(new CheckAnswers([], [], 0)));   // not read yet: the box comes with the first photo

        api.MapPost("/varer/{id:int}/tog", (string boxCode, int id, FundUseCases fund) => Change(boxCode, id, fund, () => fund.TakeAsync(id)));
        api.MapPost("/varer/{id:int}/nej", (string boxCode, int id, FundUseCases fund) => Change(boxCode, id, fund, () => fund.DeclineAsync(id)));
        api.MapPost("/varer/{id:int}/fortryd", (string boxCode, int id, FundUseCases fund) => Change(boxCode, id, fund, () => fund.BackAsync(id)));

        api.MapGet("/varer/{id:int}/foto", async (string boxCode, int id, FundUseCases fund, ItemUseCases items) =>
            await fund.InBoxAsync(id, boxCode) && await items.PhotoFileAsync(id) is { } file ? Results.File(file, "image/jpeg") : Results.NotFound());
    }

    private static async Task<IResult> Change(string boxCode, int id, FundUseCases fund, Func<Task> change)
    {
        if (!await fund.InBoxAsync(id, boxCode)) return Results.NotFound();
        await change();
        return Results.Ok();
    }

    // The interesting-line without the medal sign (no emojis in Vareinfo): "🏆 GULD – ..." -> "GULD – ...".
    private static CheckAnswer Answer(FundRow r) => new(r.Item.Id, r.Item.Line(), r.Item.Category, r.Item.Condition, r.Item.Quantity,
        new string((r.Note ?? "").SkipWhile(c => !char.IsLetterOrDigit(c)).ToArray()), r.Kr, r.Item.PricesAt is null,
        r.Item.ItemPhoto != "", r.Item.PhotoId);
}

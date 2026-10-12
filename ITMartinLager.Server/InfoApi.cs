using ITMartinLager.Application;
using ITMartinLager.Domain;

namespace ITMartinLager.Server;

// The API Vareinfo calls (2026-10-12 split): pick or make a box, put what was read into it, and the kolofon round trip.
// Only with Vareinfo's key (X-Info-Key = Lager__InfoKey) - the check is in Program.cs. Each endpoint calls a use case.
public static class InfoApi
{
    public sealed record BoxDto(int Id, string Code, string Place);
    public sealed record NewBox(string Place);
    public sealed record Intake(int? PhotoId, List<Found> Items);
    public sealed record WantedDto(int ItemId, string BoxCode, string Line, string Title, bool FirstEditionLook, int TopKr);
    public sealed record KolofonRead(KolofonReading? Reading);

    private static BoxDto Dto(Box b) => new(b.Id, b.Code, b.Place);

    public static void MapInfoApi(this WebApplication app)
    {
        var api = app.MapGroup("/api/info");

        api.MapGet("/kasser", async (int? antal, BoxUseCases boxes) =>
            (await boxes.RecentAsync(Math.Clamp(antal ?? 8, 1, 50))).Select(Dto));

        api.MapGet("/kasser/{code}", async (string code, BoxUseCases boxes) =>
            await boxes.ByCodeAsync(code) is { } b ? Results.Ok(Dto(b)) : Results.NotFound());

        api.MapPost("/kasser", async (NewBox body, BoxUseCases boxes) => Dto(await boxes.NewAsync(body.Place ?? "")));

        api.MapPost("/kasser/{code}/varer", async (string code, Intake body, BoxUseCases boxes, RegistrationUseCases reg) =>
        {
            if (await boxes.ByCodeAsync(code) is not { } box) return Results.NotFound();
            var found = (body.Items ?? []).Where(f => !string.IsNullOrWhiteSpace(f.Title))
                .Select(f => f with { Kind = Kinds.Normalise(f.Kind), Condition = Conditions.Normalise(f.Condition), Quantity = Math.Max(1, f.Quantity) })
                .ToList();
            var (added, merged) = await reg.SaveAsync(box.Id, body.PhotoId, found);
            return Results.Ok(new { Added = added, Merged = merged });
        });

        api.MapGet("/kolofon", async (KolofonUseCases k) => new
        {
            k.FromKr,
            Books = (await k.WantedAsync()).Select(w => new WantedDto(w.Item.Id, w.Item.Box?.Code ?? "", w.Item.Line(), w.Item.Title,
                w.Item.EditionHint == EditionHints.First, w.TopKr)),
        });

        api.MapPost("/varer/{id:int}/kolofon/foto", async (int id, KolofonUseCases k) =>
            await k.TakenAsync(id) ? Results.Ok() : Results.NotFound());

        api.MapPost("/varer/{id:int}/kolofon", async (int id, KolofonRead body, KolofonUseCases k) =>
            await k.ReadAsync(id, body.Reading) ? Results.Ok() : Results.NotFound());
    }
}

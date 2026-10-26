namespace ITMartinKunder.Server;

// What the apps call (through ITMartinKunder.Client). 400 + { error } = a real "no" the person should see; anything
// else not 2xx makes the app keep the message in its outbox and send it again.
internal static class KunderEndpoints
{
    public static void MapKunderApi(this WebApplication app)
    {
        app.MapPost(KunderApi.SignUp, async (SignUpRequest r, Kundekartotek k) => Answer(await k.SignUpAsync(r))).DisableAntiforgery();
        app.MapPost(KunderApi.SignUpWithCode, async (SignUpRequest r, Kundekartotek k) =>
        {
            var (error, code) = await k.SignUpWithCodeAsync(r);
            return error is null ? Results.Ok(new SignUpResponse(code)) : Results.BadRequest(new ErrorResponse(error));
        }).DisableAntiforgery();
        app.MapPost(KunderApi.CheckCode, async (CodeCheckRequest r, Kundekartotek k, FreeApps apps) =>
            Results.Ok(await k.CheckCodeAsync(r.Code, r.App, await apps.AllAsync()))).DisableAntiforgery();
        app.MapPost(KunderApi.Bought, async (PurchaseRequest r, Kundekartotek k) => Answer(await k.BoughtAsync(r))).DisableAntiforgery();
        app.MapPost(KunderApi.Import, async (ImportRequest r, Kundekartotek k) => Answer(await k.ImportAsync(r))).DisableAntiforgery();
        app.MapPost(KunderApi.Unsubscribe, async (UnsubscribeRequest r, Kundekartotek k) =>
            Results.Ok(new UnsubscribeResponse(await k.UnsubscribeAsync(r.Email)))).DisableAntiforgery();
        app.MapPost(KunderApi.UnsubscribeLink, async (UnsubscribeLinkRequest r, Kundekartotek k) =>
            await k.UnsubscribeByLinkAsync(r.Token) is { } email ? Results.Ok(new UnsubscribeResponse(true, email)) : Results.Ok(new UnsubscribeResponse(false)))
            .DisableAntiforgery();

        app.MapGet(KunderApi.FreeApps, async (FreeApps apps) => Results.Ok(await apps.AvailableAsync()));
        app.MapGet(KunderApi.Helpers, async (Kundekartotek k) => Results.Ok(await k.HelpersAsync()));

        // "Hent mine data" for one person (Martin's cookie) - he sends the file to whoever asked.
        app.MapGet("/kunder/{id:int}.json", async (int id, Kundekartotek k) =>
            await k.ExportAsync(id) is { } x
                ? Results.File(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(x, new System.Text.Json.JsonSerializerOptions
                    { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }),
                    "application/json", $"mine-data-{id}.json")
                : Results.NotFound());

        // The ones who still said yes to that company - for the Bcc line or a mail tool (Martin's cookie).
        app.MapGet("/kunder.csv", async (string? firma, Kundekartotek k, HttpContext ctx) =>
        {
            var company = Companies.All.Contains(firma ?? "") ? firma! : Companies.ITMartin;
            static string Q(string v) => "\"" + v.Replace("\"", "\"\"") + "\"";
            var site = $"{ctx.Request.Scheme}://{ctx.Request.Host}";
            var rows = await k.ActiveAsync(company);
            var csv = "Navn;Email;Telefon;Kundenummer;Interesser;Apps;Ja givet;Afmeld-link\n" + string.Concat(rows.Select(c =>
                $"{Q(c.Name)};{Q(c.Email)};{Q(c.Phone)};{Q(c.Code)};{Q(c.Interests)};{Q(string.Join(", ", c.Apps))};" +
                $"{c.Consents.First(x => x.Company == company).At:yyyy-MM-dd HH:mm};{site}/afmeld/{c.UnsubscribeToken}\n"));
            return Results.File(System.Text.Encoding.UTF8.GetPreamble().Concat(System.Text.Encoding.UTF8.GetBytes(csv)).ToArray(),
                "text/csv", $"kunder-{company.ToLowerInvariant()}.csv");
        });
    }

    private static IResult Answer(string? error) => error is null ? Results.Ok() : Results.BadRequest(new ErrorResponse(error));
}

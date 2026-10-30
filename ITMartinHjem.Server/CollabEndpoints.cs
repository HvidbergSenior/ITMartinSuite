using System.Net;
using System.Security.Claims;
using ITMartinHjem.Application;
using ITMartinHjem.Domain;
using ITMartinHjem.Server.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;

namespace ITMartinHjem.Server;

// "Samarbejde" (2026-10-30): plain form posts from /per/tekster and /per/musik, each answered with a redirect back to the page.
// The other person opens the space with its code (a cookie role); Martin, logged in on Min side, opens every space.
public static class CollabEndpoints
{
    /// <summary>Who writes in this space: "Martin" for the owner, the space's name for its code holder, null for anyone else.</summary>
    public static string? Who(this ClaimsPrincipal? user, CollabSpace space) =>
        user.IsOwner() ? CollabRules.Owner : user?.IsInRole(CollabRules.RoleFor(space.Slug)) == true ? space.Name : null;

    public static void MapCollab(this WebApplication app)
    {
        app.MapGet("/samarbejde/{space}/login", (string space, string? back, string? err, CollabSpaces spaces) =>
            spaces.Find(space) is { } s ? Results.Content(LoginPage(s, Back(s, back), err is not null), "text/html") : Results.NotFound());

        app.MapPost("/samarbejde/{space}/login", async (string space, HttpContext ctx, CollabSpaces spaces) =>
        {
            if (spaces.Find(space) is not { } s) return Results.NotFound();
            var form = await ctx.Request.ReadFormAsync();
            var back = Back(s, form["back"]);
            if (!CollabRules.CodeFits(s, form["code"]))
            {
                await Task.Delay(800);   // slow down guessing
                return Results.Redirect($"/samarbejde/{s.Slug}/login?err=1&back={Uri.EscapeDataString(back)}");
            }
            var claims = new List<Claim> { new(ClaimTypes.Name, s.Name), new(ClaimTypes.Role, CollabRules.RoleFor(s.Slug)) };
            await ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)),
                new AuthenticationProperties { IsPersistent = true });
            return Results.Redirect(back);
        }).DisableAntiforgery();

        app.MapPost("/samarbejde/{space}/{room}/upload", async (string space, string room, HttpContext ctx, CollabSpaces spaces, CollabUseCases collab) =>
        {
            if (spaces.Find(space) is not { } s || CollabRules.Room(room) is not { } r) return Results.NotFound();
            if (ctx.User.Who(s) is not { } by) return Results.Redirect($"/samarbejde/{s.Slug}/login?back=/{s.Slug}/{r.Slug}");
            var form = await ctx.Request.ReadFormAsync();
            var file = form.Files["file"];
            string? error;
            if (file is null) error = "Vælg en fil først.";
            else
            {
                await using var stream = file.OpenReadStream();
                error = await collab.UploadAsync(s.Slug, r.Slug, form["title"], by, stream, file.FileName, file.ContentType, file.Length);
            }
            return Results.Redirect($"/{s.Slug}/{r.Slug}" + (error is null ? "?ok=fil" : "?fejl=" + Uri.EscapeDataString(error)));
        }).DisableAntiforgery()
          .WithMetadata(new RequestSizeLimitAttribute(100_000_000), new RequestFormLimitsAttribute { MultipartBodyLengthLimit = 100_000_000 });

        app.MapPost("/samarbejde/{space}/tanke/{id:int}", async (string space, int id, HttpContext ctx, CollabSpaces spaces, CollabUseCases collab) =>
        {
            if (spaces.Find(space) is not { } s) return Results.NotFound();
            if (await collab.FileAsync(id) is not { } f || f.Space != s.Slug) return Results.NotFound();
            if (ctx.User.Who(s) is not { } by) return Results.Redirect($"/samarbejde/{s.Slug}/login?back=/{s.Slug}/{f.Room}");
            var form = await ctx.Request.ReadFormAsync();
            var error = await collab.WriteAsync(id, by, form["text"]);
            return Results.Redirect($"/{s.Slug}/{f.Room}" + (error is null ? $"?ok=tanke#fil-{id}" : "?fejl=" + Uri.EscapeDataString(error)));
        }).DisableAntiforgery();

        app.MapGet("/samarbejde/{space}/fil/{id:int}", async (string space, int id, HttpContext ctx, CollabSpaces spaces, CollabUseCases collab) =>
        {
            if (spaces.Find(space) is not { } s || ctx.User.Who(s) is null) return Results.NotFound();
            if (await collab.OpenAsync(id, s.Slug) is not { } open) return Results.NotFound();
            var f = open.File;
            // Sound and film play in the page; texts are downloaded with their own name.
            var play = CollabRules.IsAudio(f) || CollabRules.IsVideo(f);
            ctx.Response.Headers.CacheControl = "private, max-age=3600";
            return Results.File(open.Path, f.ContentType.Length > 0 ? f.ContentType : "application/octet-stream",
                fileDownloadName: play ? null : f.FileName, enableRangeProcessing: true);
        });

        app.MapPost("/samarbejde/{space}/slet/{id:int}", async (string space, int id, HttpContext ctx, CollabSpaces spaces, CollabUseCases collab) =>
        {
            if (spaces.Find(space) is not { } s || !ctx.User.IsOwner()) return Results.NotFound();
            var f = await collab.FileAsync(id);
            if (f is not null && f.Space == s.Slug) await collab.DeleteAsync(id);
            return Results.Redirect($"/{s.Slug}/{f?.Room ?? CollabRules.Texts}?ok=slettet");
        }).DisableAntiforgery();
    }

    // Only back to a page of the same space - never to another site.
    private static string Back(CollabSpace s, string? back) =>
        back is { } b && b.StartsWith('/' + s.Slug, StringComparison.OrdinalIgnoreCase) && !b.Contains("//") ? b : "/" + s.Slug;

    private static string LoginPage(CollabSpace s, string back, bool err) => $$"""
<!DOCTYPE html>
<html lang="da" data-family="rede"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
<title>{{WebUtility.HtmlEncode(s.Name)}} og Martin · Martin Hvidberg</title>
<link rel="stylesheet" href="/_content/ITMartin.Shared.UI/css/kolibri.css"><link rel="stylesheet" href="/app.css"></head>
<body><main class="k-main"><form class="k-card k-pin" method="post" action="/samarbejde/{{s.Slug}}/login">
<input type="hidden" name="back" value="{{WebUtility.HtmlEncode(back)}}">
<div class="k-card-title">{{WebUtility.HtmlEncode(s.Name)}} og Martin</div>
<p class="k-card-sub">Skriv koden, du har fået af Martin. Så kan du lægge tekster og musik op og skrive dine tanker. Telefonen husker koden bagefter.</p>
{{(err ? "<p class=\"k-error\">Forkert kode – prøv igen.</p>" : "")}}
<input class="k-input" type="password" name="code" autocomplete="current-password" autofocus required aria-label="Kode">
<button class="k-btn k-btn-primary k-btn-block k-mt" type="submit">Åbn</button>
<p class="k-help"><a href="/{{s.Slug}}">← Tilbage</a></p>
</form></main></body></html>
""";
}

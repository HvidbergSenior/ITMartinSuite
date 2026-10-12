using ITMartin.Shared.UI.Kolibri;
using ITMartinSeMed.Server;
using ITMartinSeMed.Application;
using ITMartinSeMed.Infrastructure;
using Microsoft.AspNetCore.Mvc;

// Se med (Kolibri Kommunikere) - 2026-10-05, user: "i want to make my own Hurtig hjælp" + "i want to tell
// her what to do". The customer types a code from Martin, shares the screen in the browser, and the two
// talk through the page. View only by design: when a customer rips a CD, she must click herself (§12 stk. 4).
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddKolibri(k =>
{
    k.Name = "Se med";
    k.KolibriName = "Kolibri Kommunikere";
    k.Family = "kommunikere";
    k.Tagline = "Martin ser med på din skærm og fortæller dig, hvad du skal gøre.";
    k.About =
    [
        "Se med er hjælp over afstand. Du viser Martin din computerskærm, og I taler sammen gennem siden – så viser han dig vej, mens du selv trykker.",
        "Martin kan kun se med. Han kan ikke styre din mus eller dit tastatur, og du kan stoppe når som helst.",
        "Billedet og lyden går direkte mellem jeres to computere. Intet bliver optaget eller gemt.",
    ];
    k.HowTo =
    [
        "Ring til Martin, eller aftal et tidspunkt. Han læser en kode på 6 tal op for dig.",
        "Åbn semed.itmartin.dk i Chrome eller Edge på computeren, skriv koden og tryk Forbind.",
        "Tryk Del skærm. Vælg Hele skærmen og tryk Del. Sig ja til mikrofonen, så kan I tale sammen.",
        "Når I er færdige, trykker du Stop – eller lukker bare siden.",
    ];
    k.Version = "2026.10";
    k.ThemeColor = "#f4f6f5";
});

builder.Services.AddRazorComponents();
// DDD/Clean since 2026-10-12: the session rules are in Domain, the session flow in Application.Rooms;
// this Server only does the login and hands each WebSocket to Rooms.
builder.Services.AddSeMedApplication();

var app = builder.Build();
app.MapKolibri();

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Error");

app.UseStaticFiles();
app.UseWebSockets(new WebSocketOptions { KeepAliveInterval = TimeSpan.FromSeconds(20) });
app.UseAntiforgery();

var pin = app.Configuration["SeMed:Pin"] ?? "";
bool IsMartin(HttpContext ctx) => pin.Length >= 6 && ctx.Request.Cookies["semed_auth"] == pin;

app.MapPost("/api/login", (HttpContext ctx, [FromForm(Name = "pin")] string typed) =>
{
    if (pin.Length < 6 || typed != pin) return Results.Redirect("/martin?fejl=1");
    ctx.Response.Cookies.Append("semed_auth", pin, new CookieOptions
    {
        HttpOnly = true, Secure = ctx.Request.IsHttps, SameSite = SameSiteMode.Strict, MaxAge = TimeSpan.FromDays(365)
    });
    return Results.Redirect("/martin");
}).DisableAntiforgery();

app.MapGet("/api/logout", (HttpContext ctx) =>
{
    ctx.Response.Cookies.Delete("semed_auth");
    return Results.Redirect("/martin");
});

// Behind the Cloudflare tunnel every request comes from cloudflared, so the visitor is in this header.
static string ClientIp(HttpContext ctx) =>
    ctx.Request.Headers["CF-Connecting-IP"].FirstOrDefault() ?? ctx.Connection.RemoteIpAddress?.ToString() ?? "?";

app.Map("/ws", async (HttpContext ctx, Rooms rooms) =>
{
    if (!ctx.WebSockets.IsWebSocketRequest) return Results.BadRequest();
    var role = ctx.Request.Query["rolle"].ToString();
    if (role == "martin" && !IsMartin(ctx)) return Results.Unauthorized();

    using var ws = await ctx.WebSockets.AcceptWebSocketAsync();
    var peer = new WebSocketPeer(ws);
    if (role == "martin")
    {
        var s = await rooms.HelperArrivedAsync(peer, ctx.Request.Query["kode"]);
        var ended = await peer.ReadAsync(text => rooms.ForwardAsync(s, peer, text));
        await rooms.HelperLeftAsync(s, peer, ended);
    }
    else
    {
        var s = await rooms.CustomerArrivedAsync(peer, ctx.Request.Query["kode"], ClientIp(ctx));
        if (s is null) { await peer.CloseAsync(); return Results.Empty; }
        await peer.ReadAsync(text => rooms.ForwardAsync(s, peer, text));
        await rooms.CustomerLeftAsync(s, peer);
    }
    return Results.Empty;
});

app.MapRazorComponents<App>()
    .AddAdditionalAssemblies(typeof(ITMartin.Shared.UI.Components.Kolibri.KolibriOm).Assembly);

app.Run();

public partial class Program;

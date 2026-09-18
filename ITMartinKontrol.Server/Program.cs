using ITMartinKontrol.Server;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<DockerClient>();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

// Only containers listed under Kontrol:Apps (Kontrol__Apps__N in compose) can
// be touched. Everything else on the socket is unreachable through this API.
var allowed = app.Configuration.GetSection("Kontrol:Apps").Get<string[]>() ?? [];
var pin = app.Configuration["Kontrol:Pin"];
if (string.IsNullOrWhiteSpace(pin))
    app.Logger.LogWarning("Kontrol__Pin is not set - start/stop endpoints will refuse every request.");

bool PinOk(HttpRequest req) =>
    !string.IsNullOrWhiteSpace(pin)
    && req.Headers.TryGetValue("X-Kontrol-Pin", out var v)
    && string.Equals(v.ToString(), pin, StringComparison.Ordinal);

app.MapGet("/api/apps", async (DockerClient docker) =>
{
    var states = await Task.WhenAll(allowed.Select(n => docker.InspectAsync(n)));
    return Results.Ok(states);
});

app.MapPost("/api/apps/{name}/{action}", async (string name, string action, HttpRequest req, DockerClient docker) =>
{
    if (!allowed.Contains(name, StringComparer.Ordinal)) return Results.NotFound();
    if (!PinOk(req)) return Results.StatusCode(401);

    var ok = action switch
    {
        "start" => await docker.StartAsync(name),
        "stop"  => await docker.StopAsync(name),
        _       => false
    };
    if (!ok) return Results.Problem($"Docker refused to {action} {name}.");

    // Give the daemon a moment so the caller gets the new state back, not the old.
    await Task.Delay(500);
    return Results.Ok(await docker.InspectAsync(name));
});

app.MapGet("/health", () => Results.Ok("ok"));

app.Run();

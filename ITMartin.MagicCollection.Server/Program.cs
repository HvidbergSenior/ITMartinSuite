using System.Text.Json.Serialization;
using ITMartin.Magic.Application.Interfaces;
using ITMartin.Magic.Domain.Entities;
using ITMartin.Magic.Infrastructure;
using ITMartin.Magic.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);
builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);

var connectionString = builder.Configuration.GetConnectionString("MagicDb")
    ?? "Host=localhost;Database=magic;Username=postgres";

builder.Services.AddMagicPersistence(connectionString);
builder.Services.AddScoped<IMagicCardRepository, MagicCardRepository>();

builder.Services.AddHttpClient("scryfall", c =>
{
    c.BaseAddress = new Uri("https://api.scryfall.com/");
    c.DefaultRequestHeaders.Add("User-Agent", "ITMartinMagicCollection/1.0 (contact: hvidbergsenior@gmail.com)");
    c.DefaultRequestHeaders.Add("Accept", "application/json");
});

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/lookup", async (string name, IHttpClientFactory factory, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(name)) return Results.BadRequest();

    var http = factory.CreateClient("scryfall");
    var response = await http.GetAsync($"cards/named?fuzzy={Uri.EscapeDataString(name)}", ct);

    if (!response.IsSuccessStatusCode)
        return Results.NotFound(new { error = "Kort ikke fundet" });

    var dto = await response.Content.ReadFromJsonAsync<ScryfallCardResult>(ct);
    if (dto is null) return Results.NotFound();

    return Results.Ok(new
    {
        scryfallId      = dto.Id,
        name            = dto.Name,
        setCode         = dto.Set,
        setName         = dto.SetName,
        collectorNumber = dto.CollectorNumber,
        imageUrl        = dto.ImageUris?.Normal,
        eurPrice        = ParsePrice(dto.Prices?.Eur),
        eurFoil         = ParsePrice(dto.Prices?.EurFoil),
        usdPrice        = ParsePrice(dto.Prices?.Usd),
        manaCost        = dto.ManaCost,
        typeLine        = dto.TypeLine,
        rarity          = dto.Rarity,
    });
});

app.MapPost("/api/save", async (SaveRequest req, IMagicCardRepository repo, CancellationToken ct) =>
{
    var card = new MagicCard
    {
        Id              = Guid.NewGuid(),
        Name            = req.Name,
        SetCode         = req.SetCode,
        CollectorNumber = req.CollectorNumber,
        ScryfallId      = req.ScryfallId,
        EurPrice        = req.EurPrice,
        UsdPrice        = req.UsdPrice,
        Quantity        = 1,
        FirstSeenAt     = DateTime.UtcNow,
        LastSeenAt      = DateTime.UtcNow,
    };
    await repo.UpsertScannedAsync(card, ct);
    return Results.Ok();
});

app.MapGet("/api/saved", async (IMagicCardRepository repo, CancellationToken ct) =>
{
    var all    = await repo.GetAllAsync(ct);
    var recent = all.OrderByDescending(c => c.LastSeenAt).Take(50);
    return Results.Ok(recent.Select(c => new
    {
        c.Id, c.Name, c.SetCode, c.CollectorNumber,
        eurPrice   = c.EurPrice,
        usdPrice   = c.UsdPrice,
        quantity   = c.Quantity,
        lastSeenAt = c.LastSeenAt,
    }));
});

app.Run();

static decimal? ParsePrice(string? value) =>
    decimal.TryParse(value,
        System.Globalization.NumberStyles.Any,
        System.Globalization.CultureInfo.InvariantCulture,
        out var d)
        ? d : null;

record SaveRequest(
    string  ScryfallId,
    string  Name,
    string  SetCode,
    string  CollectorNumber,
    decimal? EurPrice,
    decimal? UsdPrice);

class ScryfallCardResult
{
    [JsonPropertyName("id")]             public string Id              { get; set; } = "";
    [JsonPropertyName("name")]           public string Name            { get; set; } = "";
    [JsonPropertyName("set")]            public string Set             { get; set; } = "";
    [JsonPropertyName("set_name")]       public string SetName         { get; set; } = "";
    [JsonPropertyName("collector_number")] public string CollectorNumber { get; set; } = "";
    [JsonPropertyName("mana_cost")]      public string? ManaCost       { get; set; }
    [JsonPropertyName("type_line")]      public string? TypeLine       { get; set; }
    [JsonPropertyName("rarity")]         public string? Rarity         { get; set; }
    [JsonPropertyName("image_uris")]     public ScryfallImageUris? ImageUris { get; set; }
    [JsonPropertyName("prices")]         public ScryfallPrices? Prices { get; set; }
}

class ScryfallImageUris
{
    [JsonPropertyName("normal")] public string? Normal { get; set; }
    [JsonPropertyName("small")]  public string? Small  { get; set; }
}

class ScryfallPrices
{
    [JsonPropertyName("eur")]      public string? Eur     { get; set; }
    [JsonPropertyName("eur_foil")] public string? EurFoil { get; set; }
    [JsonPropertyName("usd")]      public string? Usd     { get; set; }
}

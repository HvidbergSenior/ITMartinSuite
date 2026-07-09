var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHttpClient("internal", c => c.Timeout = TimeSpan.FromSeconds(6));

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

// Checked over the internal martinnet Docker network (container:8080), not the
// public domain - avoids a Cloudflare round-trip and tells us the real state
// even for apps with no public route configured yet.
var showcaseTargets = new Dictionary<string, string>
{
    ["star-realms"] = "http://star-realms-web:8080/",
    ["budget"] = "http://budget-web:8080/login",
    ["magic"] = "http://magic-web:8080/",
    ["cloudoverblik"] = "http://cloudoverblik-web:8080/",
    ["stats"] = "http://stats-web:8080/"
};

app.MapGet("/api/showcase-status", async (IHttpClientFactory httpFactory) =>
{
    var client = httpFactory.CreateClient("internal");
    // Concurrent writes from parallel tasks below - plain Dictionary isn't
    // thread-safe for that and was silently dropping/corrupting entries.
    var results = new System.Collections.Concurrent.ConcurrentDictionary<string, bool>();

    await Task.WhenAll(showcaseTargets.Select(async kv =>
    {
        try
        {
            var resp = await client.GetAsync(kv.Value);
            results[kv.Key] = resp.IsSuccessStatusCode || (int)resp.StatusCode is >= 300 and < 400;
        }
        catch
        {
            results[kv.Key] = false;
        }
    }));

    return Results.Ok(results);
});

app.MapGet("/api/links", (IConfiguration cfg) => Results.Ok(new
{
    fileSorter      = cfg["Apps:FileSorterUrl"]      ?? "#",
    gallery         = cfg["Apps:GalleryUrl"]         ?? "#",
    curator         = cfg["Apps:CuratorUrl"]         ?? "#",
    scan            = cfg["Apps:ScanUrl"]            ?? "#",
    musik           = cfg["Apps:MusikUrl"]           ?? "#",
    musikStudio     = cfg["Apps:MusikStudioUrl"]     ?? "#",
    budget          = cfg["Apps:BudgetUrl"]          ?? "#",
    receipt         = cfg["Apps:ReceiptUrl"]         ?? "#",
    magic           = cfg["Apps:MagicUrl"]           ?? "#",
    library         = cfg["Apps:LibraryUrl"]         ?? "#",
    librarySearch   = cfg["Apps:LibrarySearchUrl"]   ?? "#",
    magicCollection = cfg["Apps:MagicCollectionUrl"] ?? "#",
    magazine        = cfg["Apps:MagazineUrl"]        ?? "#",
    magazineSearch  = cfg["Apps:MagazineSearchUrl"]  ?? "#",
    family          = cfg["Apps:FamilyUrl"]          ?? "#",
    adhd            = cfg["Apps:AdhdUrl"]            ?? "#",
    club            = cfg["Apps:ClubUrl"]            ?? "#",
    bartab          = cfg["Apps:BarTabUrl"]          ?? "#",
    auction         = cfg["Apps:AuctionUrl"]         ?? "#",
    market          = cfg["Apps:MarketUrl"]          ?? "#",
    imageGen        = cfg["Apps:ImageGenUrl"]        ?? "#",
    r6Assistant     = cfg["Apps:R6AssistantUrl"]     ?? "#",
    r6Intel         = cfg["Apps:R6IntelUrl"]         ?? "#",
    cloudOverblik   = cfg["Apps:CloudOverblikUrl"]   ?? "#",
    upload          = cfg["Apps:UploadUrl"]          ?? "#",
    testHub         = cfg["Apps:TestHubUrl"]         ?? "#",
}));

app.Run();

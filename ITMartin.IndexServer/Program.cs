var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/links", (IConfiguration cfg) => Results.Ok(new
{
    fileSorter      = cfg["Apps:FileSorterUrl"] ?? "#",
    gallery         = cfg["Apps:GalleryUrl"]    ?? "#",
    budget          = cfg["Apps:BudgetUrl"]      ?? "#",
    receipt         = cfg["Apps:ReceiptUrl"]     ?? "#",
    magic           = cfg["Apps:MagicUrl"]       ?? "#",
    library         = cfg["Apps:LibraryUrl"]     ?? "#",
    family          = cfg["Apps:FamilyUrl"]      ?? "#",
    adhd            = cfg["Apps:AdhdUrl"]        ?? "#",
    bartab          = cfg["Apps:BarTabUrl"]      ?? "#",
    auction         = cfg["Apps:AuctionUrl"]     ?? "#",
    market          = cfg["Apps:MarketUrl"]      ?? "#",
    testHub         = cfg["Apps:TestHubUrl"]     ?? "#",
}));

app.Run();

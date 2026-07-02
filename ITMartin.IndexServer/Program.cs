var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

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

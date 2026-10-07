using Microsoft.EntityFrameworkCore;

namespace ITMartinLager.Server.Data;

// What kind of thing it is. Stored as text, so a new kind never breaks old rows.
public static class Kinds
{
    public const string Cd = "CD", Dvd = "DVD", BluRay = "Blu-ray", Magasin = "Magasin", AndersAnd = "Anders And-blad",
        Jumbobog = "Jumbobog", Tegneserie = "Tegneserie", Konsolspil = "Konsolspil", Bog = "Bog",
        Magic = "Magic-kort", Andet = "Andet";

    public static readonly string[] All = [AndersAnd, Jumbobog, Tegneserie, Magasin, Bog, Cd, Dvd, BluRay, Konsolspil, Magic, Andet];

    public static string Icon(string kind) => kind switch
    {
        Cd => "🎵", Dvd or BluRay => "💿", Magasin => "📰", AndersAnd or Jumbobog => "🦆", Tegneserie => "💥",
        Konsolspil => "🎮", Bog => "📚", Magic => "🃏", _ => "📦",
    };

    public static string Normalise(string? kind) =>
        All.FirstOrDefault(k => string.Equals(k, kind?.Trim(), StringComparison.OrdinalIgnoreCase)) ?? Andet;
}

public static class Conditions
{
    public static readonly string[] All = ["Som ny", "Meget god", "God", "Slidt", "Defekt"];
    public static string Normalise(string? c) =>
        All.FirstOrDefault(x => string.Equals(x, c?.Trim(), StringComparison.OrdinalIgnoreCase)) ?? "God";
}

public static class Statuses
{
    public const string Lager = "På lager", TilSalg = "Til salg", PaaAuktion = "På auktion", Solgt = "Solgt";
    public static readonly string[] All = [Lager, TilSalg, PaaAuktion, Solgt];
}

// A box, shelf or crate with a printed QR label - the only "where is it" the register needs.
public sealed class Box
{
    public int Id { get; set; }
    public string Code { get; set; } = "";       // K0001 - on the label
    public string Place { get; set; } = "";      // "Garagen, reol 3"
    public string Note { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class Item
{
    public int Id { get; set; }
    public string Kind { get; set; } = Kinds.Andet;
    public string Title { get; set; } = "";
    public string Series { get; set; } = "";     // "Anders And & Co.", "Fantomet", "PlayStation 2"
    public string Number { get; set; } = "";     // issue / volume / card number, as printed
    public int? Year { get; set; }
    public string Platform { get; set; } = "";   // console games
    public string Artist { get; set; } = "";     // CDs (and author for books)
    public string Condition { get; set; } = "God";
    public int Quantity { get; set; } = 1;
    public int? BoxId { get; set; }
    public Box? Box { get; set; }
    public int? PhotoId { get; set; }
    public decimal? Price { get; set; }          // kr, set by a person
    public decimal? PriceHint { get; set; }      // kr, suggested by a source (Scryfall for Magic) - never the selling price by itself
    public string MagicRef { get; set; } = "";
    public string Barcode { get; set; } = "";    // EAN/ISBN when scanned   // card id in the MTG Scanner, so a new fetch updates instead of duplicating
    public string Status { get; set; } = Statuses.Lager;
    public string Note { get; set; } = "";
    // What makes it interesting - the shop's focus (user 2026-10-05: "it is the interest we focus on"), shown even
    // when the item is cheap: who drew it, which story is in it, what was special that year. AI draft, edited by hand.
    public string Interest { get; set; } = "";
    public double Confidence { get; set; } = 1;
    public string ItemPhoto { get; set; } = "";  // own photo of just this item (for auctions/shop), relative to the data dir

    // Sales. AuctionItemId = the item's id in Live Auktion while it is up for auction; the result is copied here,
    // because Live Auktion deletes an auction 7 days after it was created.
    public string AuctionItemId { get; set; } = "";
    public string AuctionCode { get; set; } = "";
    public decimal? SoldPrice { get; set; }
    public string SoldTo { get; set; } = "";
    public DateTime? SoldAt { get; set; }  // how sure the AI was; < 0.6 = check it
    public string Search { get; set; } = "";     // lower-case title + series + number + year, for LIKE search
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public void Touch()
    {
        UpdatedAt = DateTime.UtcNow;
        Search = string.Join(' ', Title, Artist, Series, Number, Year?.ToString() ?? "", Platform, Kind, Note, Interest, Barcode).ToLowerInvariant();
    }
}

// One registration photo (kept, so a wrong reading can be checked against the picture).
public sealed class Photo
{
    public int Id { get; set; }
    public string Path { get; set; } = "";       // relative to the data dir
    public int? BoxId { get; set; }
    public int Found { get; set; }
    public DateTime TakenAt { get; set; } = DateTime.UtcNow;
}

// Every book that has been valued on the Bøger page, kept or not (user 2026-10-07: "save all automatically") -
// a searchable record of what was checked and what it was worth, apart from the box register (Items).
public sealed class BookCheck
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Author { get; set; } = "";
    public int? Year { get; set; }
    public string Condition { get; set; } = "God";
    public string Isbn { get; set; } = "";
    public int Quantity { get; set; } = 1;
    public int? PhotoId { get; set; }
    public string Verdict { get; set; } = "";    // Sælg / Kasse / Genbrug / Tjek selv
    public int? Low { get; set; }                // kr, null = Tjek selv
    public int? High { get; set; }
    public string Demand { get; set; } = "";
    public string SellAt { get; set; } = "";     // DBA / AbeBooks / Amazon.de / eBay / Bogshoppen
    public string Reason { get; set; } = "";
    public string Dba { get; set; } = "";        // the listing texts as shown, at the time of the check
    public string AbeBooks { get; set; } = "";
    public string AmazonDe { get; set; } = "";
    public string BoxCode { get; set; } = "";    // set when it was saved into a box
    public string Search { get; set; } = "";
    public DateTime CheckedAt { get; set; } = DateTime.UtcNow;

    public void Touch() => Search = string.Join(' ', Title, Author, Year?.ToString() ?? "", Isbn, Verdict, SellAt).ToLowerInvariant();
}

// AI calls per day - the hard cap on spend lives in the database, so a restart does not reset it.
public sealed class AiDay
{
    public DateOnly Day { get; set; }
    public int Calls { get; set; }
    public long InputTokens { get; set; }
    public long OutputTokens { get; set; }
}

public sealed class LagerDb(DbContextOptions<LagerDb> options) : DbContext(options)
{
    public DbSet<Box> Boxes => Set<Box>();
    public DbSet<Item> Items => Set<Item>();
    public DbSet<Photo> Photos => Set<Photo>();
    public DbSet<AiDay> AiDays => Set<AiDay>();
    public DbSet<BookCheck> BookChecks => Set<BookCheck>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Box>().HasIndex(x => x.Code).IsUnique();
        b.Entity<Item>().HasIndex(x => x.Search);
        b.Entity<Item>().HasIndex(x => x.Kind);
        b.Entity<Item>().HasIndex(x => x.BoxId);
        b.Entity<Item>().Property(x => x.Price).HasConversion<double?>();
        b.Entity<Item>().Property(x => x.SoldPrice).HasConversion<double?>();
        b.Entity<Item>().Property(x => x.PriceHint).HasConversion<double?>();
        b.Entity<Item>().HasIndex(x => x.MagicRef);
        b.Entity<Item>().HasIndex(x => x.AuctionItemId);
        b.Entity<AiDay>().HasKey(x => x.Day);
        b.Entity<BookCheck>().HasIndex(x => x.Search);
        b.Entity<BookCheck>().HasIndex(x => x.CheckedAt);
    }
}

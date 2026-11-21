namespace ITMartinVarer.Contracts;

// The shop API (/api/butik/*) between Lager (the stock) and Hjem (bogshoppen.dk shows it). Key header X-Shop-Key.
// Never anything a buyer should not see: no boxes, notes, buyers or purchase prices.
public static class ShopHeaders
{
    public const string Key = "X-Shop-Key";
}

/// <summary>One thing in the shop. BuyNowKr null = no Køb nu yet (ExpectKr = what it is worth on the other sites,
/// minus 10 %). Available = copies not reserved by someone paying right now. Type = ProductTypes. DayKr = today's
/// price when a day offer covers it (DayOffer says which). Badge = "🏆 GULD" / "🥈 SØLV" / "🥉 BRONZE" or "". Usage = Usages.
/// ForShow = from a test box (user 2026-10-24): shown, never sold - no Køb nu, no kurv. Interested = how many customers
/// marked it "interessant" on the marketplace.</summary>
public sealed record ShopItem(int Id, string Kind, string Line, string Title, string Interest, string Condition, int? Year,
    int? BuyNowKr, int? ExpectKr, int Available, bool HasPhoto, string Type = "", int? DayKr = null, string DayOffer = "", string Badge = "",
    string Usage = "", bool ForShow = false, int Interested = 0, ShopProof? Reference = null, string Deal = "", bool Assess = false,
    string Seller = "", int Photos = 1, ShopProof? NewReference = null);

/// <summary>The proof for the price promise: one listing on another site.</summary>
public sealed record ShopProof(string Source, int Kr, string Original, string Url, bool IsNew);

// A sale from payment to the buyer having it (user 2026-10-20: "sold, packed, in storage or something").
public static class OrderStatuses
{
    public const string Pending = "Afventer betaling", Paid = "Betalt", Packed = "Pakket", Sent = "Sendt", PickedUp = "Afhentet",
        Expired = "Udløbet", Cancelled = "Annulleret";

    /// <summary>Paid for - whatever has happened since.</summary>
    public static bool IsPaid(string status) => status is Paid or Packed or Sent or PickedUp;
}

public sealed record ShopOrder(string Status, string Line, int Kr, int PaidKr, string Delivery, string Email, string Tracking = "")
{
    public bool Paid => OrderStatuses.IsPaid(Status);
    public bool Expired => Status == OrderStatuses.Expired;
}

public sealed record ShopSearch(bool CanPay, List<ShopItem> Items);
public sealed record ShopItemPage(bool CanPay, ShopItem Item, List<ShopProof> Proof);
public sealed record ShopFeatured(ShopItem Item, List<ShopProof> Proof);
public sealed record ShopFront(bool CanPay, List<ShopFeatured> Items);
/// <summary>Køb nu for one thing (ItemIds null = the id in the path) or the whole basket.</summary>
public sealed record ShopBuy(string SiteBase, List<int>? ItemIds = null);
public sealed record ShopBuyAnswer(string? Url, string? Error);

// The marketplace (user 2026-10-20: "a place where they easily can see products in different categories").

/// <summary>A product type with how many things are for sale in it.</summary>
public sealed record ShopType(string Type, int Count);

/// <summary>A subgroup inside a type, from the data: a series ("Jonah Hex") or an artist/author, and how many.</summary>
public sealed record ShopGroup(string Name, int Count);

/// <summary>Today's offers, as the buyer reads them: "Hver mandag: 50 % på bøger".</summary>
public sealed record ShopOffer(string Name, string Text, string When);

/// <summary>One page of the marketplace: the types (with counts), today's offers, and the things that match.</summary>
public sealed record ShopMarket(bool CanPay, List<ShopType> Types, List<ShopOffer> Today, List<ShopItem> Items, int Total,
    List<ShopGroup>? Groups = null, List<ShopPack>? Packs = null, Dictionary<string, int>? Genres = null);

/// <summary>A pack from a per-piece deal, sold online as one thing (user 2026-11-02: "online sale need to have some kind of
/// rare and batch of", chose fixed packs): Count mixed things for Kr kr, and how many such packs we can make (Left).</summary>
public sealed record ShopPack(string Name, int Count, int Kr, int Left);

/// <summary>bogshoppen.dk's overview (user 2026-11-03: "i need all in bogshoppen but not as buttons... Should be more simple"):
/// every main category with how many things it has and its first few (dearest first), plus the packs. KindCounts = priced
/// things per kind (DVD, CD ...), LanguageCounts = per language per category - for the shops' subcategories (2026-11-03).</summary>
public sealed record ShopShelf(string Type, int Count, List<ShopItem> Items);
public sealed record ShopOverview(List<ShopShelf> Shelves, List<ShopPack> Packs, Dictionary<string, int>? KindCounts = null,
    Dictionary<string, Dictionary<string, int>>? LanguageCounts = null);

/// <summary>A basket priced for today: each copy, what it costs today and why, and the total.</summary>
public sealed record ShopQuote(bool CanPay, List<ShopQuoteLine> Lines, int Total, int Saved, List<ShopOffer> Today, List<int> Gone,
    ShopRabat? Rabat = null);

/// <summary>Mængderabat for the page (2026-11-16, "the amount should be reflected on the page for user"): how many things, the
/// percent now, how many more for the next step and its percent (null at the top), and every step for the little table.</summary>
/// <summary>Mængderabat steps and the flea-market bundle answer (user 2026-11-16) - the one rule Salg and bogshoppen.dk share.</summary>
public static class RabatRules
{
    public static readonly IReadOnlyList<(int From, int Percent)> Steps = [(5, 10), (10, 20), (20, 30), (50, 40), (100, 50)];
    public static int PercentFor(int count) => Steps.LastOrDefault(s => count >= s.From).Percent;
    public static (int More, int Percent)? Next(int count) =>
        Steps.FirstOrDefault(s => s.From > count) is { From: > 0 } n ? (n.From - count, n.Percent) : null;

    /// <summary>"Hvad vil du have for det hele?" (user 2026-11-16: "like a flea market", "a random percent... 5-15"): on top of the
    /// mængderabat, a random extra between these.</summary>
    public const int ExtraFrom = 5, ExtraTo = 15;

    /// <summary>A price a customer is told: down to the nearest 5 kr ("17 -> 15"), never up; under 5 kr whole kroner, at least 1.</summary>
    public static int Round(decimal kr) => kr < 5 ? (int)Math.Max(1, Math.Floor(kr)) : (int)(Math.Floor(kr / 5) * 5);

    /// <summary>The bundle answer: the basket total after mængderabat, then the extra percent off, rounded down to 5 kr.</summary>
    public static int BundleKr(int totalAfterRabat, int extraPercent) => Round(totalAfterRabat * (100 - extraPercent) / 100m);
}

public sealed record ShopRabat(int Count, int Percent, int? NextMore, int? NextPercent, List<ShopRabatStep> Steps);
public sealed record ShopRabatStep(int From, int Percent);
public sealed record ShopQuoteLine(int Id, string Line, int ListKr, int Kr, string Offer);

// "Interessant" (user 2026-10-24): a customer logged in with the kundenummer marks things - no buying yet.

/// <summary>Mark or unmark a thing for this kundenummer (the site checked it in Kunder first).</summary>
public sealed record ShopInterestMark(string Customer);
/// <summary>After pressing: whether it is marked now, how many have it marked, or the reason in Danish.</summary>
public sealed record ShopInterest(bool Marked, int Count, string? Error = null);
/// <summary>The things this kundenummer has marked.</summary>
public sealed record ShopMine(List<int> ItemIds);

/// <summary>Sort orders for the marketplace.</summary>
public static class ShopSorts
{
    public const string Newest = "nyeste", Cheapest = "billigst", Dearest = "dyrest", Title = "titel";
    public static readonly string[] All = [Newest, Cheapest, Dearest, Title];
}

/// <summary>One step of a deal: Count things for Kr kr in all.</summary>
public sealed record ShopDealStep(int Count, int Kr);

/// <summary>A deal for one group of kinds, with its steps: "Bøger: 2 for 50 · 5 for 100 kr".</summary>
/// AlsoSeries = series of other kinds that are in it too ("Basserne" is a Tegneserie). EveryPrice = every thing that matches
/// is in it whatever its own price (a fixed price per piece) - only gold, silver and bronze keep their own, and with OwnFromKr
/// also a sure thing whose exact copies cost that much or more.
public sealed record ShopDeal(string Group, IReadOnlyList<string> Kinds, IReadOnlyList<ShopDealStep> Steps,
    IReadOnlyList<string>? AlsoSeries = null, bool EveryPrice = false, int? OwnFromKr = null, IReadOnlyList<int>? PackSizes = null,
    string PackWord = "blade")
{
    /// <summary>The packs sold online from this deal, with how many full packs these things make.</summary>
    public List<ShopPack> Packs(int things) => (PackSizes ?? []).Where(n => things >= n)
        .Select(n => new ShopPack($"Pakke: {n} blandede {PackWord} – {Group}", n, n * Steps[0].Kr / Steps[0].Count, things / n)).ToList();

    public string Name => Steps is [{ Count: 1 } one]
        ? $"{Group}: {one.Kr} kr stk."
        : $"{Group}: {string.Join(" · ", Steps.Select(s => $"{s.Count} for {s.Kr}"))} kr";

    /// <summary>A thing of this kind and series is in the deal.</summary>
    public bool Matches(string kind, string? series) =>
        Kinds.Contains(kind) || (series is { Length: > 0 } s && AlsoSeries?.Contains(s.Trim(), StringComparer.OrdinalIgnoreCase) == true);

    /// <summary>What one thing costs in the first step (2 for 50 = 25 kr). A thing priced at or under this is sold in the
    /// deal - else it would cost less alone than in the deal (user 2026-10-31: "books should not cost 20 kr one place and
    /// 2 for 50 another").</summary>
    public int MaxSingleKr => Steps[0].Kr / Steps[0].Count;
}

/// <summary>One price range in the price search: Key in the URL, Name on the button.</summary>
public sealed record ShopPriceBand(string Key, string Name, int From, int? Below);

/// <summary>The deals as their own categories (user 2026-10-31, "lets say 50 and 100 kr. Books 2 for 50 5 for 100. Anders
/// and and basserne and similar 4 for 50 10 for 100. Dvd 4 for 50 10 for 100"): the things of these kinds without a price
/// or priced at or under the deal's MaxSingleKr, unless gold, silver or bronze. Which thing is in which deal is Lager's rule
/// (Shop.DealFor); a list shows one band: a deal's Name, Single (priced, no deal), NoPrice, or null (all).</summary>
public static class ShopDeals
{
    public static readonly ShopDeal Books = new("Bøger", [Kinds.Bog], [new(2, 50), new(5, 100)]);
    public static readonly ShopDeal Blade = new("Blade", [Kinds.AndersAnd, Kinds.Tegneserie, Kinds.Superhelte], [new(4, 50), new(10, 100)]);
    public static readonly ShopDeal Dvds = new("DVD", [Kinds.Dvd], [new(4, 50), new(10, 100)]);
    /// <summary>User 2026-10-31 "VHS Bundles... for a price" - 4 for 50 10 for 100 like DVD (user said yes 2026-10-31).</summary>
    public static readonly ShopDeal Vhs = new("VHS", [Kinds.Vhs], [new(4, 50), new(10, 100)]);
    /// <summary>User 2026-10-31 yes to "should CDs get the same deal?" - 4 for 50 10 for 100.</summary>
    public static readonly ShopDeal Cds = new("CD", [Kinds.Cd], [new(4, 50), new(10, 100)]);
    public static readonly IReadOnlyList<ShopDeal> All = [Blade, Books, Dvds, Vhs, Cds];

    /// <summary>User 2026-11-02 after the loppemarked: "anders and blade and basserne should be sold for 3 kr a piece... So
    /// only if rare blad there should be links" - every Anders And-blad and every Basserne for 3 kr, whatever its own price;
    /// a rare one (gold, silver, bronze) keeps its own price and its links. Same day: "if an exact copy costs like 25+ then
    /// they should be on the page" - OwnFromKr 25 (PriceHint = exact copies). The 3 kr ones are not on bogshoppen.dk.</summary>
    public static readonly ShopDeal PerPiece = new("Anders And og Basserne", [Kinds.AndersAnd], [new(1, 3)], AlsoSeries: ["Basserne"], EveryPrice: true,
        OwnFromKr: 25, PackSizes: [10, 25]);

    /// <summary>User 2026-11-16: "all vhs should be sold for 5 kr no pictures" - every VHS 5 kr a piece, like the Anders And bin: not
    /// shown one by one (so no pictures), sold as packs of 10 online. A person-marked rare one keeps its own price.</summary>
    public static readonly ShopDeal VhsPerPiece = new("VHS", [Kinds.Vhs], [new(1, 5)], EveryPrice: true, PackSizes: [10], PackWord: "VHS-film");

    /// <summary>Anders And-blade with their own price shown on bogshoppen.dk: only the dearest this many (user 2026-11-02: "there
    /// must always ONLY be shown 10-20 rare anders and blade").</summary>
    public const int MaxRareAndersAndOnPage = 20;

    /// <summary>Every deal there is (PerPiece + All) - for names and the category chips.</summary>
    public static readonly IReadOnlyList<ShopDeal> Known = [PerPiece, VhsPerPiece, .. All];

    /// <summary>The deals in use: only PerPiece since 2026-11-02 (the 4 for 50 deals went 2026-10-31: "the 4 for 50 and so
    /// should be removed" - every thing its own price); All stays so they can come back by listing them here.</summary>
    // DVD back 2026-11-21 (user: "Minimum 4 dvds for 50. 10 dvds for 100 -> and you get to keep them"): single DVDs; a season or
    // box set priced over MaxSingleKr (12 kr) stays out and is sold one by one (Lager Shop.DealFor).
    // VhsPerPiece out 2026-11-21 ("remove vhs"). CD (4 for 50) + Bøger (2 for 50) on 2026-11-21 (user "yes"): 829 CDs and 296 books had
    // only the standard price - a deal is the honest loppemarked price; one priced over MaxSingleKr (CD 12, bog 25 kr) keeps its own.
    public static readonly IReadOnlyList<ShopDeal> Active = [PerPiece, Dvds, Cds, Books];

    /// <summary>The deal for things of this kind among these deals (default Active), or null.</summary>
    public static ShopDeal? For(string kind, IReadOnlyList<ShopDeal>? deals = null) => For(kind, null, deals);

    /// <summary>The deal for a thing of this kind and series among these deals (default Active), or null.</summary>
    public static ShopDeal? For(string kind, string? series, IReadOnlyList<ShopDeal>? deals = null) =>
        (deals ?? Active).FirstOrDefault(d => d.Matches(kind, series));

    public static ShopDeal? Named(string? name) => Known.FirstOrDefault(d => d.Name == name);

    public const string Single = "enkelt";
    /// <summary>Not priced and in no deal (user 2026-10-31: "No pricing yet. We are investigating"; unsure readings = a
    /// person looks - "low confidence should be manual investigation").</summary>
    public const string NoPrice = "uden-pris";
    /// <summary>"Alle" on bogshoppen.dk/produkter as a price key: every own-priced thing across all kinds, so the shop window
    /// shows things you can buy first (user 2026-11-01 "nicer and better... more easy to look at products").</summary>
    public const string WithPrice = "med-pris";
    public const string NoPriceName = "Ingen pris endnu";
    /// <summary>The unsure readings in one category of their own, without a price (user 2026-10-31: "show them in one
    /// category") - what the AI read may be another thing, so its price and reference are not shown.</summary>
    public const string Unsure = "usikker";
    public const string UnsureName = "Ikke tjekket endnu";
    /// <summary>Everything with a mark (Guld, Sølv, Bronze, Platin) in one category, dearest first (user 2026-11-02: "make
    /// these products in a categroy i can see").</summary>
    public const string Rare = "sjaeldne";
    /// <summary>Interesting things are looked at by a person (user 2026-11-03: "All the interesting ones, should be manually looked at
    /// by us and be in a special categropy", chose "50 kr+ or a mark"): a found price from this, or a mark (Guld, Sølv, Bronze,
    /// Platin), and no price a person set = no automatic price, and they are in Sjældne.</summary>
    public const int AssessFromKr = 50;
    public const string AssessText = "Sjælden – vi vurderer prisen";

    /// <summary>A person must set the price first (see AssessFromKr).</summary>
    public static bool Assess(int? personKr, int? hintKr, string badge) => personKr is not > 0 && (badge != "" || hintKr >= AssessFromKr);
    public const string RareName = "Sjældne";

    /// <summary>Price search (user 2026-10-31: "price seacrh like 10 - 29 30-49 50-99 99+"): own-priced things (no deal)
    /// from From kr up to under Below kr (null = no top).</summary>
    public static readonly IReadOnlyList<ShopPriceBand> Prices =
        [new("10-29", "10-29 kr", 10, 30), new("30-49", "30-49 kr", 30, 50), new("50-99", "50-99 kr", 50, 100), new("100+", "100 kr og op", 100, null)];

    public static ShopPriceBand? PriceBand(string? key) => Prices.FirstOrDefault(p => p.Key == key);

    /// <summary>A band Lager knows, else null (= everything).</summary>
    public static string? Band(string? band) => band is Single or NoPrice or Unsure or Rare || Named(band) is not null || PriceBand(band) is not null ? band : null;
}

// The marketplace's offers (2026-11-05): "Byd" on one thing (hidden max) and "Mit bundt" (several things, one bid or a question).
public sealed record ShopBidPlace(string Customer, int MaxKr);
public sealed record ShopBundlePlace(string Customer, List<int> ItemIds, int? Kr);
/// <summary>Ok, or the reason in Danish.</summary>
public sealed record ShopOfferAnswer(bool Ok, string? Error = null, int? AnswerKr = null, int? ExtraPercent = null);
/// <summary>The customer's own bids (item -> their max), so the page can show "Dit bud: 120 kr".</summary>
public sealed record ShopMyBids(Dictionary<int, int> MaxKr);

/// <summary>One of the customer's bundles on bogshoppen.dk (haggling, 2026-11-21). PriceForYou = the price they can say yes to now;
/// YourKr = their latest bid; Status = Ny / Besvaret / Modbud / Aftalt / Afvist; FromShop = Martin offered it.</summary>
public sealed record ShopBundleView(int Id, List<string> Lines, int? YourKr, int? PriceForYou, int? AgreedKr, string Status, bool FromShop, DateTime At);
public sealed record ShopMyBundles(List<ShopBundleView> Bundles);
/// <summary>The customer's answer: Accept = yes to PriceForYou; else Kr = a new bid; Reject = no thanks.</summary>
public sealed record ShopBundleReply(string Customer, bool Accept, int? Kr, bool Reject = false);

/// <summary>"Lån en konsol" (user 2026-11-21: "I also have a lot of ps3s they should be able to play dvds" + "take money for
/// borrowing" + PS4/PS5; prices chosen the same day, "no depositum needed"): a weekend Friday-Monday, price incl. moms. Only the console is
/// lent - films and games are SOLD (renting those out needs the rights holder's OK). A PS3 is free with 10 DVDs bought.</summary>
public static class ConsoleLoanRules
{
    public const string Ps3 = "PS3", Ps4 = "PS4", Ps5 = "PS5";
    public static readonly IReadOnlyList<(string Console, int PriceKr)> All = [(Ps3, 50), (Ps4, 100), (Ps5, 150)];
    public const int FreePs3FromDvds = 10, MaxDaysAhead = 60;
    public static (string Console, int PriceKr)? For(string? console) =>
        All.FirstOrDefault(c => c.Console == console) is { Console: not null } c ? c : null;
    /// <summary>The weekend price: a PS3 is free when they buy 10 DVDs.</summary>
    public static int? PriceFor(string? console, bool withTenDvds) =>
        For(console) is { } c ? (c.Console == Ps3 && withTenDvds ? 0 : c.PriceKr) : null;
}

public sealed record ShopLoanPlace(string Customer, string Console, DateTime Friday, bool WithTenDvds);
public sealed record ShopLoanView(int Id, string Console, DateTime Friday, int PriceKr, string Status);
public sealed record ShopMyLoans(List<ShopLoanView> Loans);

/// <summary>"Hvad syntes du?" (2026-11-21): a customer's verdict (few words) + optional teaser, first name only, shown after Martin approves.</summary>
public sealed record ShopVerdictPlace(string Customer, string Name, string Verdict, string Teaser);
public sealed record ShopVerdict(int ItemId, string Name, string Verdict, string Teaser);
public sealed record ShopVerdicts(List<ShopVerdict> Verdicts);

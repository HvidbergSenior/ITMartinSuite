namespace ITMartinLager.Domain;

// Fixed price per kind (user 2026-11-24): "i want to have this standard price BUT if a product is found to be higher on faraos and/or
// dba and the other relevant, then I need to set the price" + "dba should not be counted if other prices found. If no other then
// reference dba" + chose "typical price >= 2x standard and >= 50 kr".
// So an ordinary thing gets NO hint (= the shop shows Shop.StandardFor, the fixed price of its kind). Only when the typical used price
// found is clearly higher, the hint is that price - and a hint of 50 kr or more makes the shop say "vi vurderer prisen" until Martin
// sets the price himself (ShopDeals.Assess, AssessFromKr = 50).
public static class FixedPrice
{
    /// <summary>Used shops/markets that count. DBA only when none of the others found the thing (its asking prices run high).</summary>
    public static readonly string[] Sources = ["Faraos", "GulogGratis", "Nemos Bibliotek", "Discogs", "AbeBooks", "Accord"];
    public const string Dba = "DBA";
    public const int Factor = 2;
    public const int MinKr = 50;
    /// <summary>At least this many matching listings before a price is trusted (user 2026-11-24: "I dont believe the prices" - one
    /// listing was often another product: Lorcana for "First Wave", a bust for "Green Hornet", a trykvariant).</summary>
    public const int MinListings = 2;
    /// <summary>A used copy is at most this share of the new price (user 2026-11-24, Caza: used 250 kr while Bog & idé sells it new for 300).</summary>
    public const decimal MaxShareOfNew = 0.6m;

    /// <summary>Kinds that follow the fixed price. The rest keep their own way (Magic, Ting, box sets, console games ...).</summary>
    // Box sets and console games follow the same rules since 2026-11-24 (user: "silver in treatment?" - ONE DBA listing for seasons
    // 1 + 2 at 400 kr made every In Treatment box 200 kr).
    public static bool Applies(Item i) =>
        !i.Rare && i.Kind != Kinds.Magic && i.Kind != Kinds.Ting && StandardOf(i) is not null;

    /// <summary>The kind's fixed price; an Anders And-blad is the 3 kr bin.</summary>
    public static int? StandardOf(Item i) => i.Kind == Kinds.AndersAnd ? 3 : Shop.StandardFor(i.Kind, i.Category);

    /// <summary>The typical (middle) used price found: the other sources first, DBA only when they found nothing. Null = nothing found.</summary>
    public static int? Typical(IEnumerable<PriceFind> finds, Item? i = null)
    {
        var list = Chosen(finds, i);
        if (list.Count == 0) return null;
        list.Sort();
        return list.Count % 2 == 1 ? list[list.Count / 2] : (list[list.Count / 2 - 1] + list[list.Count / 2]) / 2;
    }

    /// <summary>Words that mean a SPECIAL version, not an ordinary copy (user 2026-11-24, Anders And 17/1973: both listings were the rare
    /// "trykvariant uden dato" - ours has the date). Such a listing only counts when our own thing says the same word.</summary>
    public static readonly string[] SpecialWords =
        ["trykvariant", "variant", "fejltryk", "uden dato", "til læg", "i pose", "billedhæfte", "guldbog", "signeret", "signed"];

    /// <summary>True when the listing is about a special version our thing is not marked as.</summary>
    public static bool SpecialVersion(string listing, Item? i) => SpecialVersion(listing, OwnText(i));

    /// <summary>Same, with our thing's own words (title, note, interest, edition) as one text.</summary>
    public static bool SpecialVersion(string listing, string ownText)
    {
        var l = (listing ?? "").ToLowerInvariant();
        var own = (ownText ?? "").ToLowerInvariant();
        return SpecialWords.Any(w => l.Contains(w) && !own.Contains(w));
    }

    public static string OwnText(Item? i) => i is null ? "" : $"{i.Title} {i.Note} {i.Interest} {i.EditionHint}";

    /// <summary>A listing for another issue (user 2026-11-24 "I dont think the matches are precise": Skipper Skræk nr. 26 from 1960 got
    /// nr. 26 from 1958 and 1962): another number or edition, or - for a numbered issue - a year in the listing that is not ours.</summary>
    public static bool OtherIssue(Item i, string listing)
    {
        listing ??= "";
        if (Pricing.OtherNumber(i, listing) || Pricing.OtherEdition(i, listing) || Pricing.OtherYear(i, listing)) return true;
        if (i.Number.Trim() == "" || i.Year is not > 0) return false;
        var years = System.Text.RegularExpressions.Regex.Matches(listing, @"\b(19|20)\d{2}\b").Select(m => int.Parse(m.Value)).ToList();
        return years.Count > 0 && !years.Contains(i.Year.Value);
    }

    /// <summary>The used prices that count: the other sources, or DBA when they found nothing. Special versions and other issues are left out.</summary>
    public static List<int> Chosen(IEnumerable<PriceFind> finds, Item? i = null)
    {
        var used = finds.Where(f => !f.IsNew && !f.Similar && f.Kr > 0 && !SpecialVersion(f.Name, i) && (i is null || !OtherIssue(i, f.Name))).ToList();
        var others = used.Where(f => Sources.Contains(f.Source)).Select(f => f.Kr).ToList();
        return others.Count > 0 ? others : used.Where(f => f.Source == Dba).Select(f => f.Kr).ToList();
    }

    /// <summary>Blade/magazines (user 2026-11-24 "this is just for magazines"): a blurry or missing photo = no photo in the shop and
    /// the fixed price - never Martin's list (a blurry cover cannot prove which issue it is).</summary>
    public static readonly string[] Magazines = [Kinds.AndersAnd, Kinds.Superhelte, Kinds.Tegneserie, Kinds.Magasin];
    public static bool BlurryMagazine(Item i) => Magazines.Contains(i.Kind) && i.ShopPhotoProblem != "";

    /// <summary>Photo rules (user 2026-11-24): books ALWAYS need a proper own photo; a special CD/DVD (own price, "vurderes" or rare)
    /// needs one too - ordinary CDs/DVDs are shown with a shared photo of a pile. Null = no photo needed, else why.</summary>
    public static string? PhotoNeeded(Item i)
    {
        var special = i.Price is > 0 || i.PriceHint >= ITMartinVarer.Contracts.ShopDeals.AssessFromKr || i.Rare;
        var needsOwn = i.Kind == Kinds.Bog || (special && i.Kind is Kinds.Cd or Kinds.Dvd or Kinds.BluRay);
        return needsOwn && i.ShopPhotoProblem != "" ? i.ShopPhotoProblem : null;
    }

    /// <summary>A book photo that is there but small (short side under this) - Martin decides if it is good enough.</summary>
    public const int SmallBookPhoto = 400;

    /// <summary>True when Martin must set the price: typical found price >= 2x the fixed price and >= 50 kr.</summary>
    public static bool NeedsOwnPrice(Item i, IEnumerable<PriceFind> finds) =>
        Applies(i) && i.Price is not > 0 && !BlurryMagazine(i) && i.Confidence >= Pricing.MinConfidence && Chosen(finds, i) is { Count: >= MinListings } && Typical(finds, i) is { } typ
        && OurPrice(typ, NewTypical(finds.ToList())) is var t && StandardOf(i) is { } s && t >= Factor * s && t >= MinKr;

    /// <summary>The hint for a thing that follows the fixed price: the typical price when Martin must decide, else none (the shop
    /// then shows the fixed price). "Som ny" keeps its own rule (25 % under the new price) when that is higher. Not applicable =
    /// the hint is left as it was.</summary>
    public static decimal? Hint(Item i, IEnumerable<PriceFind> finds, decimal? current)
    {
        if (!Applies(i)) return current;
        var list = finds.ToList();
        // Our price, not the market's: the usual 10-25 % under (Pricing.ShopPrice), and never over 60 % of the new price (2026-11-24).
        decimal? hint = NeedsOwnPrice(i, list) ? OurPrice(Typical(list, i)!.Value, NewTypical(list)) : null;
        if (Pricing.LikeNew(i.Usage) && Pricing.AsNewPrice(NewTypical(list)) is { } asNew && asNew > (hint ?? 0)) hint = asNew;
        return hint;
    }

    /// <summary>The shop price from the typical used price, capped by the new price.</summary>
    public static int OurPrice(int typicalUsed, int? newKr)
    {
        var kr = Pricing.ShopPrice(typicalUsed) ?? typicalUsed;
        if (newKr is > 0 && kr > newKr.Value * MaxShareOfNew) kr = Pricing.OnStep(newKr.Value * MaxShareOfNew);
        return kr;
    }

    private static int? NewTypical(List<PriceFind> finds)
    {
        var fresh = finds.Where(f => f.IsNew && !f.Similar && f.Kr > 0).Select(f => f.Kr).OrderBy(k => k).ToList();
        return fresh.Count == 0 ? null : fresh[fresh.Count / 2];
    }
}

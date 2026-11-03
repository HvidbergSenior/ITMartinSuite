namespace ITMartinLager.Domain;

// Kolofon (2026-10-11, user: "to save in db and get price just cover. A job can run through kolofon if needed"):
// the cover stays the only step for every item; a kolofon photo is asked for only when the edition may matter.
public static class Kolofon
{
    // A used copy somewhere at this price or more = it may be a first edition or a sought-after printing.
    public const int DefaultFromKr = 150;
    public const int PerCall = 10;

    public static void PhotoTaken(Item item, string path)
    {
        item.KolofonPhoto = path;
        item.KolofonAt = null;   // the reader takes it next
        item.Touch();
    }

    // What the AI read goes onto the item. A new ISBN = the price hunter searches again, now for exactly that edition.
    public static void Apply(Item item, KolofonReading r, DateTime now)
    {
        var isbn = Barcodes.ToIsbn13(new string((r.Isbn ?? "").Where(c => char.IsDigit(c) || c is 'X' or 'x').ToArray()));
        if (Barcodes.IsIsbn(isbn) && isbn != item.Barcode) { item.Barcode = isbn; item.PricesAt = null; }
        if (!string.IsNullOrWhiteSpace(r.Edition)) item.Edition = r.Edition.Trim();
        if (!string.IsNullOrWhiteSpace(r.Publisher)) item.Publisher = r.Publisher.Trim();
        if (r.Year is > 1450 and < 2100) item.Year = r.Year;
        if (!string.IsNullOrWhiteSpace(r.Note) && !item.Note.Contains(r.Note.Trim()))
            item.Note = (item.Note + " Kolofon: " + r.Note.Trim()).Trim();
        item.KolofonAt = now;
        item.Touch();
    }

    /// <summary>Helpers only take pictures (user 2026-11-03: "they should just be taking pictures. like Vibeke", "it could be the
    /// last page and/or the first page? You need to be able to take more than one picture"): a kolofon page read with the box's
    /// photos belongs to the book in the same box with the same ISBN, else the one whose title (and author) it names. Null =
    /// none, or more than one fits.</summary>
    public static Item? MatchPage(IReadOnlyList<Item> inBox, string title, string author, string isbn)
    {
        var books = inBox.Where(i => i.Kind is Kinds.Bog or Kinds.Tegneserie or Kinds.Jumbobog).ToList();
        var code = Barcodes.ToIsbn13(new string(isbn.Where(c => char.IsDigit(c) || c is 'X' or 'x').ToArray()));
        if (Barcodes.IsIsbn(code) && books.Where(b => b.Barcode == code).ToList() is [var byIsbn]) return byIsbn;
        var words = ListingParse.Words(title).Where(w => !ListingParse.IsNumber(w)).ToHashSet();
        var writer = ListingParse.Words(author).ToHashSet();
        // No title on the page (an old kolofon: "© Tove Ditlevsen, 1968"): the one book in the box by that author.
        if (words.Count == 0)
            return writer.Count > 0 && books.Where(b => ListingParse.Words(b.Artist).Any(writer.Contains)).ToList() is [var byAuthor] ? byAuthor : null;
        var fits = books.Where(b =>
        {
            var mine = ListingParse.Words(Pricing.CleanTitle(b.Title)).Where(w => !ListingParse.IsNumber(w)).ToList();
            if (mine.Count == 0 || !mine.All(words.Contains)) return false;
            var theirs = ListingParse.Words(b.Artist).ToList();
            return writer.Count == 0 || theirs.Count == 0 || theirs.Any(writer.Contains);
        }).ToList();
        return fits is [var one] ? one : null;
    }

    /// <summary>One more kolofon page for a book: what it says is added to what the earlier pages said ("1. udgave" from the front
    /// and "1. oplag 1968" from the back = "1. udgave; 1. oplag 1968"). The first page's picture is kept as the kolofon photo.</summary>
    public static void AddPage(Item item, KolofonReading r, string photoPath, DateTime now)
    {
        var before = item.Edition.Trim();
        var edition = (r.Edition ?? "").Trim();
        Apply(item, r with { Edition = before == "" || edition == "" || before.Contains(edition, StringComparison.OrdinalIgnoreCase)
            ? (edition == "" ? before : before == "" ? edition : before)
            : edition.Contains(before, StringComparison.OrdinalIgnoreCase) ? edition : $"{before}; {edition}" }, now);
        if (item.KolofonPhoto == "") item.KolofonPhoto = photoPath;
    }

    public static void Unreadable(Item item, DateTime now)
    {
        item.KolofonAt = now;
        item.Note = (item.Note + " Kolofon kunne ikke læses.").Trim();
        item.Touch();
    }
}

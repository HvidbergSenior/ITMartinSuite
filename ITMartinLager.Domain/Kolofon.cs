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

    public static void Unreadable(Item item, DateTime now)
    {
        item.KolofonAt = now;
        item.Note = (item.Note + " Kolofon kunne ikke læses.").Trim();
        item.Touch();
    }
}

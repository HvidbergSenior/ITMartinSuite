namespace ITMartinVarer.Contracts;

// What was read off a cover or looked up by barcode - the product info Lager stores with the item. Not saved yet.
public sealed record Found(string Kind, string Title, string Artist, string Series, string Number, int? Year,
    string Platform, string Condition, int Quantity, double Confidence, string Note, string Interest = "", string Barcode = "",
    decimal? PriceHint = null, string EditionHint = "", string Usage = "", string Publisher = "", PhotoArea? Area = null,
    string Language = "", bool Kolofon = false, string Edition = "");

// Where one thing lies in the pile photo it was read from, as parts of the width and height (0-1, left/top first) - so
// Lager can cut out a picture of just that thing (user 2026-10-31: "there should be pictures on each item").
public sealed record PhotoArea(double Left, double Top, double Right, double Bottom)
{
    /// <summary>Null when the numbers are not a real area inside the photo.</summary>
    public static PhotoArea? From(IReadOnlyList<double>? box)
    {
        if (box is not { Count: 4 }) return null;
        double l = Math.Clamp(box[0], 0, 1), t = Math.Clamp(box[1], 0, 1), r = Math.Clamp(box[2], 0, 1), b = Math.Clamp(box[3], 0, 1);
        return r - l >= 0.02 && b - t >= 0.02 ? new(l, t, r, b) : null;
    }
}

// What the AI read on one kolofon photo (the back of the title page).
public sealed record KolofonReading(string? Isbn, string? Edition, int? Year, string? Publisher, string? Note);

namespace ITMartinVarer.Contracts;

// What was read off a cover or looked up by barcode - the product info Lager stores with the item. Not saved yet.
public sealed record Found(string Kind, string Title, string Artist, string Series, string Number, int? Year,
    string Platform, string Condition, int Quantity, double Confidence, string Note, string Interest = "", string Barcode = "",
    decimal? PriceHint = null, string EditionHint = "");

// What the AI read on one kolofon photo (the back of the title page).
public sealed record KolofonReading(string? Isbn, string? Edition, int? Year, string? Publisher, string? Note);

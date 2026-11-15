namespace ITMartinLager.Domain;

/// <summary>Which fields a thing of a kind has (user 2026-11-15: "only relevant stuff"). The Vare page shows these, plus any
/// field that already holds something - so nothing the AI or a person wrote is ever hidden.</summary>
public static class ItemFields
{
    public const string Title = "Titel", Artist = "Kunstner", Series = "Serie", Number = "Nummer", Year = "År",
        Barcode = "Stregkode", EditionHint = "Omslag", Edition = "Udgave", Publisher = "Forlag", Platform = "Konsol";

    private static readonly string[] Blad = [Title, Series, Number, Year];

    /// <summary>The fields that fit this kind. Kind, Ny/brugt, Stand, Antal, Note and Status are always there.</summary>
    public static IReadOnlyList<string> For(string kind) => kind switch
    {
        Kinds.Bog => [Title, Artist, Year, Barcode, EditionHint, Edition, Publisher],
        Kinds.AndersAnd or Kinds.Magasin or Kinds.Superhelte or Kinds.Jumbobog or Kinds.Tegneserie => Blad,
        Kinds.Cd or Kinds.Vinyl or Kinds.Kassette => [Title, Artist, Year, Barcode],
        Kinds.Dvd or Kinds.BluRay or Kinds.Vhs => [Title, Year, Barcode],
        Kinds.Konsolspil => [Title, Platform, Year, Barcode],
        Kinds.Braetspil or Kinds.Kortspil or Kinds.Rollespil => [Title, Year, Publisher],
        Kinds.Magic => [Title, Series, Number],
        _ => [Title],
    };

    /// <summary>Shown on the page: it fits the kind, or it is already filled in.</summary>
    public static bool Shows(Item i, string field) => For(i.Kind).Contains(field) || HasValue(i, field);

    /// <summary>"Forfatter" for a book, "Kunstner" for music.</summary>
    public static string ArtistLabel(string kind) => kind == Kinds.Bog ? "Forfatter" : "Kunstner";

    private static bool HasValue(Item i, string field) => field switch
    {
        Title => i.Title != "",
        Artist => i.Artist != "",
        Series => i.Series != "",
        Number => i.Number != "",
        Year => i.Year is not null,
        Barcode => i.Barcode != "",
        EditionHint => i.EditionHint != "",
        Edition => i.Edition != "",
        Publisher => i.Publisher != "",
        Platform => i.Platform != "",
        _ => false,
    };
}

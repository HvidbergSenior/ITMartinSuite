namespace ITMartinGallery.Domain;

// A gallery can be closed at a given time (Galleries__N__ClosesAt, Danish local time, e.g. 2026-09-29T23:59).
// Before that moment the pages show a warning banner (/api/closing + closing.js); after it every way into
// the gallery - the page, ?g=, the API and the picture files - answers "closed". Nothing is deleted:
// the pictures stay on the NAS, and access can be reopened (the business model: pay to keep watching).
public static class ClosingRule
{
    public const string Contact = "ITMartin@Mensa.dk · 31 19 47 30";
    private static readonly TimeZoneInfo Dk = TimeZoneInfo.FindSystemTimeZoneById("Europe/Copenhagen");

    public static DateTime NowDk() => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Dk);

    public static string Warning(DateTime at) =>
        $"⚠️ Dette galleri lukker {When(at)}. Billederne bliver ikke slettet – vil du beholde adgangen, kan du købe fortsat adgang hos ITMartin: {Contact}.";

    public static string ClosedMessage => "Galleriet er lukket. Kontakt ITMartin: " + Contact;

    private static string When(DateTime at)
    {
        var da = new System.Globalization.CultureInfo("da-DK");
        var day = at.Date == NowDk().Date ? "i aften" : at.ToString("dddd 'den' d. MMMM", da);
        return $"{day} kl. {at:HH.mm}";
    }
}

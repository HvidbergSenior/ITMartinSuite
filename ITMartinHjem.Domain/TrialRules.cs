namespace ITMartinHjem.Domain;

// "Prøv gratis" on Mine apps (user 2026-10-12/21): people leave their e-mail, become a customer in the Kundekartotek
// (Kunder) and get access from Martin by mail. ONE sign-up = ALL the free apps Martin has switched on in Kunder; the
// other apps cannot be tried for free. NO ads, NO newsletters - and it must be obvious what they sign up to.
public static class TrialRules
{
    public const int MaxName = 60, MaxEmail = 120;

    // Stored with the customer as proof of what they agreed to (markedsføringsloven §10, GDPR art. 7).
    public const string ConsentText =
        "Ja tak – skriv mig op som kunde hos ITMartin, så jeg kan bruge alle de gratis apps og nemt få jeres tilbud. " +
        "Ingen reklamer, ingen nyhedsbreve – og jeg kan afmelde mig når som helst.";

    public const string NotFree = "Den app kan ikke prøves gratis lige nu – skriv til Martin i chatten, så finder vi ud af det.";

    /// <summary>Only the apps Martin has switched on in Kunder (user 2026-10-21: "all others cannot be tried for free").</summary>
    public static bool IsFree(string app, IEnumerable<string> freeApps) =>
        freeApps.Any(f => string.Equals(f.Trim(), app.Trim(), StringComparison.OrdinalIgnoreCase));

    public static string Source(string app) => $"itmartin.dk: Prøv gratis – {app}";

    // The message Martin gets in Svar (and as a push); his answer there goes to the person by mail.
    public static string RequestMessage(string app, string email) =>
        $"Jeg vil gerne bruge de gratis apps (startede ved {app}). Send mig adgang på {email} – min kundekode står i Kunder.";
}

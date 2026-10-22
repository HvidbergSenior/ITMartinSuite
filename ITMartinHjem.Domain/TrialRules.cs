namespace ITMartinHjem.Domain;

// "Prøv gratis" on Mine apps (user 2026-10-12/21): people leave their e-mail, become a customer in the Kundekartotek
// (Kunder) and get access from Martin by mail. ONE sign-up = ALL the free apps Martin has switched on in Kunder; the
// other apps cannot be tried for free. NO ads, NO newsletters - and it must be obvious what they sign up to.
public static class TrialRules
{
    public const int MaxName = 60, MaxEmail = 120;

    // Stored with the customer as proof of what they agreed to (markedsføringsloven §10, GDPR art. 7).
    public const string ConsentText = SignUpRules.ConsentText;

    public const string NotFree = "Den app kan ikke prøves gratis lige nu – skriv til Martin i chatten, så finder vi ud af det.";

    /// <summary>The sign-up box on the itmartin.dk front page (user 2026-10-22: "on first page there should be a way to signup
    /// as customer") - not one app, the same yes to all the free apps.</summary>
    public const string FrontPage = "itmartin.dk";

    /// <summary>Only the apps Martin has switched on in Kunder (user 2026-10-21: "all others cannot be tried for free"),
    /// or the front page.</summary>
    public static bool IsFree(string app, IEnumerable<string> freeApps) =>
        app == FrontPage || freeApps.Any(f => string.Equals(f.Trim(), app.Trim(), StringComparison.OrdinalIgnoreCase));

    public static string Source(string app) => app == FrontPage ? "itmartin.dk: Bliv kunde – forsiden" : $"itmartin.dk: Prøv gratis – {app}";

    // The message Martin gets in Svar (and as a push); his answer there goes to the person by mail.
    public static string RequestMessage(string app, string email) =>
        $"Ny kunde: vil bruge de gratis apps (startede ved {app}), {email}. Kundenummeret er sendt på mail – står også i Kunder.";

    /// <summary>When Kunder did not answer: the number comes later, Martin sends it.</summary>
    public static string RequestMessageNoCode(string app, string email) =>
        $"Ny kunde: vil bruge de gratis apps (startede ved {app}), {email}. Kunder svarede ikke - send kundenummeret fra Kunder, når den er der.";
}

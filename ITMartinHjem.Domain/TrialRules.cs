namespace ITMartinHjem.Domain;

// "Prøv gratis" on every app on Mine apps (user 2026-10-12): people leave their e-mail, become a customer in the
// Kundekartotek and get access (a PIN) from Martin by mail. NO ads, NO newsletters - and it must be obvious what they sign up to.
public static class TrialRules
{
    public const int MaxName = 60, MaxEmail = 120;

    // Stored with the customer as proof of what they agreed to (markedsføringsloven §10, GDPR art. 7).
    public static string ConsentText(string app) =>
        $"Ja tak – skriv mig op som kunde hos ITMartin, så jeg kan prøve {app} gratis og nemt få jeres tilbud. " +
        "Ingen reklamer, ingen nyhedsbreve – og jeg kan afmelde mig når som helst.";

    public static string Source(string app) => $"itmartin.dk: Prøv gratis – {app}";

    // The message Martin gets in Svar (and as a push); his answer there goes to the person by mail.
    public static string RequestMessage(string app, string email) =>
        $"🆓 Jeg vil gerne prøve {app} gratis. Send mig adgang (PIN) på {email}.";
}

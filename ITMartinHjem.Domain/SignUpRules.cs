namespace ITMartinHjem.Domain;

// One customer = everything (user 2026-10-21: Bogshoppen and ITMartin become the same company; "Bogshoppens customers also
// get free access to the apps" and "ITMartin's customers get access to LiveAuctions"). The same yes-text on both sites,
// the kundenummer opens the free apps now and the live auctions when they open (November/December).
public static class SignUpRules
{
    // Stored with the customer as proof of what they agreed to (markedsføringsloven §10, GDPR art. 7).
    public const string ConsentText =
        "Ja tak – skriv mig op som kunde hos ITMartin og Bogshoppen. Så får jeg adgang til de gratis apps og et login til liveauktionerne, " +
        "og I husker, hvad jeg interesserer mig for, så det er nemt for mig at få jeres tilbud. " +
        "Ingen reklamer, ingen nyhedsbreve – og jeg kan afmelde mig når som helst.";

    /// <summary>"48217730" -> "4821 7730".</summary>
    public static string Show(string code) => code.Length == 8 ? code[..4] + " " + code[4..] : code;

    /// <summary>The mail the person gets at once with their kundenummer (plain text, Danish).</summary>
    public static (string Subject, string Body) WelcomeMail(string name, string code, IEnumerable<string> appsWithCode) =>
        ("Dit kundenummer hos ITMartin og Bogshoppen",
         $"""
         Hej{(name.Trim().Length > 0 ? " " + name.Trim() : "")}

         Tak fordi du har skrevet dig op. Dit kundenummer er:

         {Show(code)}

         Med dit kundenummer kan du bruge de ekstra ting i de gratis apps: {string.Join(", ", appsWithCode)}.
         Skriv nummeret, når appen beder om det. Du finder alle apps på itmartin.dk under Mine apps.

         Når liveauktionerne på bogshoppen.dk åbner i november og december, er det også dit login dér.

         Ingen reklamer og ingen nyhedsbreve. Vil du ikke være med mere, så skriv din e-mail på kunder.itmartin.dk/afmeld.

         Mange hilsner
         Martin og Rico
         ITMartin og Bogshoppen · ITMartin@Mensa.dk · 31 19 47 30
         """);
}

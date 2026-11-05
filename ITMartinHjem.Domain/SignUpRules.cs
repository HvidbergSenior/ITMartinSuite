namespace ITMartinHjem.Domain;

// One customer = everything (user 2026-10-21: Bogshoppen and ITMartin become the same company; "Bogshoppens customers also
// get free access to the apps" and "ITMartin's customers get access to LiveAuctions"). The same yes-text on both sites,
// the kundenummer opens the free apps now and the live auctions when they open (November/December).
public static class SignUpRules
{
    // Stored with the customer as proof of what they agreed to (markedsføringsloven §10, GDPR art. 7).
    public const string ConsentText =
        "Ja tak – skriv mig op som interesseret hos ITMartin og Bogshoppen. Så får jeg adgang til de gratis apps og et login til Bogshoppen, " +
        "og I husker de produkter, jeg trykker på, så det er nemt for mig at få jeres tilbud. " +
        "Ingen reklamer, ingen nyhedsbreve – og jeg kan afmelde mig når som helst.";

    /// <summary>"48217730" -> "4821 7730".</summary>
    public static string Show(string code) => code.Length == 8 ? code[..4] + " " + code[4..] : code;

    /// <summary>The note to Martin on every sign-up, from both sites (user 2026-10-28: "when people sign up i dont get mails" -
    /// bogshoppen.dk sign-ups told him nothing, itmartin.dk ones only by Svar/push).</summary>
    public static (string Subject, string Body) OwnerMail(string site, string name, string email, string what, string code)
    {
        var who = name.Trim().Length > 0 ? name.Trim() : email;
        return ($"Ny kunde på {site}: {who}",
            $"""
            {who} har skrevet sig op på {site}.

            Navn: {(name.Trim().Length > 0 ? name.Trim() : "(ikke skrevet)")}
            E-mail: {email}
            Interesseret i: {(what.Trim().Length > 0 ? what.Trim() : "(intet valgt)")}
            Kundenummer: {(code.Length > 0 ? Show(code) : "(kommer, når Kunder svarer)")}

            Se kunden i Kunder: https://kunder.itmartin.dk
            """);
    }

    /// <summary>The mail the person gets at once with their kundenummer (plain text, Danish).</summary>
    public static (string Subject, string Body) WelcomeMail(string name, string code, IEnumerable<string> appsWithCode) =>
        ("Dit nummer hos ITMartin og Bogshoppen",
         $"""
         Hej{(name.Trim().Length > 0 ? " " + name.Trim() : "")}

         Tak fordi du har skrevet dig op. Dit nummer er:

         {Show(code)}

         Med dit nummer kan du bruge de ekstra ting i de gratis apps: {string.Join(", ", appsWithCode)}.
         Skriv nummeret, når appen beder om det. Du finder dem alle på itmartin.dk under Mine hjælpere.

         Det er også dit login på bogshoppen.dk, hvor vi finder nye måder at sælge på – ikke en traditionel webshop.

         Ingen reklamer og ingen nyhedsbreve. Vil du ikke være med mere, så skriv din e-mail på kunder.itmartin.dk/afmeld.

         Mange hilsner
         Martin og Rico
         ITMartin og Bogshoppen · ITMartin@Mensa.dk · 31 19 47 30
         """);
}

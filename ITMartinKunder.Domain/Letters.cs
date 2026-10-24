namespace ITMartinKunder.Domain;

// Mails Martin sends from the list page (user 2026-10-24: "the customers need to know that I received their subscription").
// Service mails about what they said yes to - never ads or newsletters. Each letter goes ONCE per customer: sending it is
// an event on the customer (Kind "Mail", Ref = the letter's key), so a second press never mails anyone twice.
public static class Letters
{
    public const string Received = "tilmelding-modtaget";

    /// <summary>Who still waits for the letter: a yes that stands, a kundenummer, and not mailed this letter before.</summary>
    public static bool Waits(Customer c, string key) => c.CodeWorks && !c.Events.Any(e => e.Kind == EventKinds.Mailed && e.Ref == key);

    /// <summary>The "vi har fået din tilmelding" mail, text approved by the user 2026-10-24.</summary>
    public static (string Subject, string Body) ReceivedMail(Customer c, string kunderSite) =>
        ("Vi har fået din tilmelding – det får du",
         $"""
         Hej{(c.Name.Trim().Length > 0 ? " " + c.Name.Trim() : "")}

         Tak fordi du har skrevet dig op hos ITMartin og Bogshoppen. Vi har fået din tilmelding, og du er på listen.

         Dit kundenummer er: {KundeWords.ShowCode(c.Code)}
         Det er dit login til det hele.

         Det får du:
         - De gratis apps på itmartin.dk under Mine apps: ElPriser (se hvornår strømmen er billigst, og få besked på telefonen), Temperatur (den målte temperatur ved den nærmeste vejrstation) og Stem (lav dine egne afstemninger).
         - Snart: et lager, hvor du kan se vores varer og markere dem, du synes er interessante. Så ved vi, hvad folk gerne vil have.
         - Liveauktioner på bogshoppen.dk i november og december. Dit kundenummer er dit login.

         Det får du ikke: reklamer, nyhedsbreve eller dine oplysninger givet videre til andre.

         Vi gemmer kun dit navn, din e-mail, hvad du interesserer dig for, og hvornår du sagde ja. Du kan altid få at se, hvad vi har om dig, eller få det rettet eller slettet. Skriv til ITMartin@Mensa.dk. Hele teksten står på itmartin.dk/vilkaar.

         Vil du ikke være med mere, så tryk her: {kunderSite.TrimEnd('/')}/afmeld/{c.UnsubscribeToken}

         Mange hilsner
         Martin og Rico
         ITMartin og Bogshoppen · ITMartin@Mensa.dk · 31 19 47 30
         """);

    public static CustomerEvent Sent(string key, string subject, DateTime utcNow) => CustomerEvent.Of(EventKinds.Mailed, "Kunder", key, subject, 0, utcNow);
}

namespace ITMartinKunder.Domain;

// Mails Martin sends from the list page (user 2026-10-24: "the customers need to know that I received their subscription").
// Service mails about what they said yes to - never ads or newsletters. Each letter goes ONCE per customer: sending it is
// an event on the customer (Kind "Mail", Ref = the letter's key), so a second press never mails anyone twice.
public static class Letters
{
    public const string Received = "tilmelding-modtaget";

    /// <summary>Who still waits for the letter: a yes that stands, a kundenummer, and not mailed this letter before.</summary>
    public static bool Waits(Customer c, string key) => c.CodeWorks && !c.Events.Any(e => e.Kind == EventKinds.Mailed && e.Ref == key);

    /// <summary>The "vi har fået din tilmelding" mail, text approved by the user 2026-10-24 (+ what the 3 free apps do now and "made with you", same day).</summary>
    public static (string Subject, string Body) ReceivedMail(Customer c, string kunderSite) =>
        ("Vi har fået din tilmelding – det får du",
         $"""
         Hej{(c.Name.Trim().Length > 0 ? " " + c.Name.Trim() : "")}

         Tak fordi du har skrevet dig op hos ITMartin og Bogshoppen. Vi har fået din tilmelding, og du er på listen.

         Dit kundenummer er: {KundeWords.ShowCode(c.Code)}
         Det er dit login til det hele.

         Det får du – tre gratis apps, som du finder på itmartin.dk under Mine apps:

         ElPriser – elpriser.itmartin.dk
         - Se hvornår strømmen er billigst i dag og i morgen.
         - Se hvornår det er billigst at starte vaskemaskinen eller opvaskeren, og hvor mange timer du skal sætte den til.
         - Med dit kundenummer: få besked på telefonen, når strømmen er billig.

         Temperatur – temperatur.itmartin.dk
         - Den målte temperatur ved den vejrstation, der er nærmest dig – ikke en vejrudsigt, men hvad termometeret viser lige nu.

         Stem – stem.itmartin.dk
         - Med dit kundenummer kan du lave dine egne afstemninger og sende linket til familien, foreningen eller vennerne.
         - Tre slags spørgsmål: vælg et svar (fx Ja/Nej eller A/B), giv karakter fra 1 til 5, eller find de dage, hvor alle kan.
         - Gratis kan du have én åben afstemning ad gangen. Den lukker efter 14 dage.

         Og snart:
         - Et lager, hvor du kan se vores varer og markere dem, du synes er interessante. Så ved vi, hvad folk gerne vil have.
         - Liveauktioner på bogshoppen.dk i november og december. Dit kundenummer er dit login.

         Apps bliver lavet sammen med jer
         Alle apps er lavet for at hjælpe dig – ikke for at sælge dig noget. De bliver bedre, når du siger, hvad du mangler.
         Har du et spørgsmål, et ønske, en idé, eller noget der kan gøres bedre? Stil det på itmartin.dk/spoergsmaal.
         Svaret kommer i chatten, og er det noget, andre også kan bruge, kommer det op på siden – uden dit navn.

         Det får du ikke: reklamer, nyhedsbreve eller dine oplysninger givet videre til andre.

         Vi gemmer kun dit navn, din e-mail, hvad du interesserer dig for, og hvornår du sagde ja. Du kan altid få at se, hvad vi har om dig, eller få det rettet eller slettet. Skriv til ITMartin@Mensa.dk. Hele teksten står på itmartin.dk/vilkaar.

         Vil du ikke være med mere, så tryk her: {kunderSite.TrimEnd('/')}/afmeld/{c.UnsubscribeToken}

         Mange hilsner
         Martin og Rico
         ITMartin og Bogshoppen · ITMartin@Mensa.dk · 31 19 47 30
         """);

    public static CustomerEvent Sent(string key, string subject, DateTime utcNow) => CustomerEvent.Of(EventKinds.Mailed, "Kunder", key, subject, 0, utcNow);
}

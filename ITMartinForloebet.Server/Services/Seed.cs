using System.Text.Json;
using ITMartinForloebet.Server.Data;
using ITMartinForloebet.Server.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ITMartinForloebet.Server.Services;

/// <summary>The first forløb on the site is the owner's own (Movia, 26-0399). Written
/// by hand once - no AI call at startup - so an empty database still has something to read.</summary>
public static class Seed
{
    public const string MoviaSlug = "kontrolafgift-movia-2026";

    public static async Task EnsureAsync(ForloebetDbContext db)
    {
        if (await db.Forloeb.AnyAsync()) return;

        static DateTime D(int y, int m, int d) => new(y, m, d, 12, 0, 0, DateTimeKind.Utc);

        var f = new Forloeb
        {
            Slug = MoviaSlug,
            EditKey = Slugs.EditKey(),
            Titel = "Kontrolafgift – var ikke i bussen",
            Modpart = "Movia",
            Omraade = "Kontrolafgift",
            StartedAt = D(2026, 6, 29),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            ClosedAt = D(2026, 9, 18),
            Udfald = Udfald.Frafaldet,
            UdfaldTekst = "Movia frafaldt afgiften den 16.–17. september 2026, uden at der var kommet ny dokumentation i sagen. Ankenævnet lukkede klagesagen den 18. september. Spørgsmålet om videooptagelsen er stadig åbent som en indsigtsanmodning efter GDPR artikel 15 til Movia og Keolis.",
            Laering = "Du skal ikke bevise, at du ikke var der – modparten skal dokumentere, at det var dig. Bed om dokumentationen i stedet for at forklare dig.\n" +
                      "Tro-og-love-erklæringen ændrede ingenting – Movia fastholdt alligevel. Krav, der ikke står i reglerne, flytter ikke sagen, uanset om du opfylder dem.\n" +
                      "Gem hver eneste sætning modparten skriver – 'videoen er sikret' blev det vigtigste spor i sagen, tre måneder senere.",
            Delt = true,
            Trin =
            [
                new Trin { At = D(2026, 6, 29), Part = Part.Modpart, Tekst = "En kontrollør i bus 7A udstedte kontrolafgift nr. 26059882 til en passager uden ID. Passageren oplyste et navn og en fødselsdato, som kontrolløren slog op i CPR, og svarede på et kontrolspørgsmål om indflytningsdato. Borgeren var ikke i bussen." },
                new Trin { At = D(2026, 7, 2), Part = Part.Borger, Tekst = "Borgeren kontaktede Movia, oplyste at en anden person havde misbrugt hans oplysninger, og politianmeldte forholdet som identitetsmisbrug (anmeldelseskvittering med journalnummer samme dag)." },
                new Trin { At = D(2026, 7, 6), Part = Part.Modpart, Tekst = "Movia svarede, at afgiften kun kunne fjernes, hvis borgeren inden 13. juli sendte dokumentation for, hvor han befandt sig på kontroltidspunktet, en tro-og-love-erklæring og et journalnummer.", Citat = "… inden den 13.07.2026 skulle sende os en form for dokumentation på, hvor han befandt sig på kontroltidspunktet, en tro-og-love-erklæring samt et journalnummer …" },
                new Trin { At = D(2026, 7, 6), Part = Part.Borger, Tekst = "Borgeren svarede samme dag og spurgte, hvorfor det var ham, der skulle bevise, hvor han var, og ikke Movia, der skulle dokumentere, at det var ham. Han oplyste, at politiet havde sagt, at anmeldelsen var nok." },
                new Trin { At = D(2026, 7, 7), Part = Part.Modpart, Tekst = "Movia oplyste, at sagen ikke ville blive undersøgt yderligere, hvis borgeren ikke sendte dokumentation for identitetstyveri." },
                new Trin { At = D(2026, 7, 24), Part = Part.Modpart, Tekst = "Movia fastholdt afgiften. En sagsbehandler beskrev identifikationsproceduren som fire trin (navn og fødselsdag slås op i CPR via en app, adresse bekræftes, kontrolspørgsmål stilles ud fra CPR-oplysninger, spørgsmålet blev besvaret korrekt) og gentog kravet om en tro-og-love-erklæring.", Citat = "Der er i øvrigt sikret en videooptagelse af kontrolsituationen i bussen, som kan rekvireres af politiet hos operatøren." },
                new Trin { At = D(2026, 7, 27), Part = Part.Borger, Tekst = "Borgeren sendte alligevel en tro-og-love-erklæring om, at han ikke var passager den 29. juni, og henviste igen til politianmeldelsen. Afgiften blev ikke fjernet." },
                new Trin { At = D(2026, 8, 15), Part = Part.Borger, Tekst = "I august klagede borgeren til Ankenævnet for Bus, Tog og Metro (sag 26-0399) og betalte klagegebyr." },
                new Trin { At = D(2026, 9, 4), Part = Part.Modpart, Tekst = "Movia svarede Ankenævnet: afgiften fastholdes, fordi borgeren ikke har fremlagt dokumentation for sin påstand. Movia henviste til kontrollørens gå-seddel, hvoraf det fremgår, at der blev spurgt om indflytningsdato, og fandt det sandsynligt, at der også blev stillet andre spørgsmål – uden at dokumentere hvilke.", Citat = "Da klager end ikke har forsøgt at fremlægge dokumentation, der kan underbygge hans påstand, er der ikke grundlag for at ændre afgørelsen." },
                new Trin { At = D(2026, 9, 8), Part = Part.Borger, Tekst = "Borgeren svarede på Movias bemærkninger: journalnummeret er politiets dokumentation, indflytningsdatoen kan findes i offentlige registre ud fra adressen, og Movia har stadig ikke lagt de konkrete registreringer fra kontrollen frem." },
                new Trin { At = D(2026, 9, 14), Part = Part.Modpart, Tekst = "Movia afgav supplerende bemærkninger til Ankenævnet: et journalnummer dokumenterer kun, at der findes en sag, ikke hvad der er anmeldt; Movia kan ikke selv indhente oplysninger om politisagen." },
                new Trin { At = D(2026, 9, 17), Part = Part.Modpart, Tekst = "Movia frafaldt afgiften over for Ankenævnet. Samme dag oplyste busoperatøren Keolis skriftligt, at videooptagelsen ikke længere findes, og at video som udgangspunkt slettes inden for 14 dage.", Citat = "Videoen findes ikke længere hos Keolis; video slettes som udgangspunkt inden for 14 dage." },
                new Trin { At = D(2026, 9, 18), Part = Part.Anden, Tekst = "Ankenævnet lukkede sagen, da Movia ikke længere havde et krav, og henviste spørgsmål om videoovervågning til Movia og Datatilsynet." },
                new Trin { At = D(2026, 9, 18), Part = Part.Borger, Tekst = "Borgeren sendte en indsigtsanmodning efter GDPR artikel 15 til Movias databeskyttelsesrådgiver og en parallel til Keolis: hvilken dato bad Movia om at få videoen sikret, hvad skete der med den, og hvilke registreringer findes fra identifikationen. Svarfrist én måned." },
            ]
        };

        var a = new Analyse
        {
            HvadHandledeDetOm = "Movia udstedte en kontrolafgift til en person, der oplyste borgerens navn og fødselsdato og svarede på et kontrolspørgsmål. Borgeren var ikke i bussen. Sagen kom til at handle om, hvem der skal dokumentere identifikationen – og om en videooptagelse, som Movia oplyste var sikret, men som operatøren senere oplyste var slettet.",
            HvadSkulleManGoere = "- Bestride afgiften skriftligt inden for fristen og bede om dokumentationen for identifikationen – ikke forklare sig.\n- Politianmelde identitetsmisbrug, så der findes et journalnummer.\n- Klage til Ankenævnet for Bus, Tog og Metro, når Movia fastholder.\n- Notere hver sætning modparten skriver, især om beviser (\"videoen er sikret\").",
            HvadKraeves = "- Klage til Movia inden 14 dage fra afgiften (ellers rykkergebyr).\n- Klage til Ankenævnet inden for nævnets frist og med klagegebyr (tilbagebetales hvis man får medhold).\n- Skriftlighed og sagsnummer på alt.\n- Ved indsigtsanmodning: oplyse nok til at identificere sig selv (navn, fødselsdato) – ikke CPR-nummer.",
            JaOgNejTil = "Nej: Dokumentation for hvor du selv befandt dig (alibi) og tro-og-love-erklæring som betingelse – de står ikke i rejsereglerne som krav, og i dette forløb ændrede erklæringen ingenting.\nNej: At bevise sit eget fravær (alibi). Movia skal som myndighed dokumentere, at det var borgeren.\nNej: At oplyse CPR-nummer i almindelig mail.\nJa: At politianmelde og oplyse journalnummeret.\nJa: At oplyse navn og fødselsdato i en indsigtsanmodning, så Movia kan finde registreringerne.",
            UnoedigTid = "- 11 uger, politiet, Ankenævnet og mindst tre sagsbehandlere for en sag, der endte med frafald uden ny dokumentation.\n- Kontrolspørgsmålet (indflytningsdato) kan slås op i offentlige registre ud fra adressen – den ekstra kontrol gav ikke den sikkerhed, Movia beskrev.\n- Movia kunne have lagt de konkrete registreringer fra appen frem i første svar. Var de der, havde sagen været afgjort på dag 25; var de der ikke, havde Movia frafaldet på dag 25.\n- Oplysningen om en sikret video kostede tre måneder, før operatøren oplyste, at den var slettet efter 14 dage.",
            Love = "- Lov om trafikselskaber § 29 (kontrolafgift, Movias hjemmel).\n- Fælles landsdækkende rejseregler pkt. 2.6 (kontrol af rejsehjemmel) og 2.7.4 (identifikation).\n- Forvaltningsloven §§ 22–24 (begrundelse) – Movia er en forvaltningsmyndighed.\n- Databeskyttelsesforordningen art. 12 (frist én måned), 15 (indsigt), 16 (berigtigelse), 18 (begrænsning); databeskyttelsesloven § 11 (CPR-numre).\n- Ankenævnet for Bus, Tog og Metro – vedtægter om klagefrist og gebyr.",
            Udkast = [],
            Lignende = [],
            Resume = "En kontrolafgift blev udstedt til en person, der udgav sig for borgeren; kontrolløren slog navn og fødselsdato op i CPR og stillede ét kontrolspørgsmål. Borgeren bestred, politianmeldte og klagede til Ankenævnet – og bad hele vejen om dokumentation frem for at bevise sit fravær. Efter 11 uger frafaldt Movia afgiften uden ny dokumentation. Læring: bed om dokumentationen, sig nej til krav der ikke står i reglerne, og gem hver sætning modparten skriver."
        };
        f.AnalyseJson = JsonSerializer.Serialize(a);
        f.Resume = a.Resume;

        db.Forloeb.Add(f);
        db.SiteState.Add(new SiteState
        {
            Overskrift = "1 forløb om kontrolafgift – frafaldet efter 11 uger, uden at modparten lagde dokumentation frem",
            OverskriftHash = "seed",
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }
}

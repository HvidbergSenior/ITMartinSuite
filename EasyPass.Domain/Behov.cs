namespace EasyPass.Domain;

/// <summary>What a place has. Ja = it is there, Nej = it is not, Ukendt = nobody has checked yet.</summary>
public enum Svar { Ukendt = 0, Ja = 1, Nej = 2 }

/// <summary>One thing a place can have (or not), with the Danish words people see.</summary>
/// <summary>Spoergsmaal = what to ask the place when nobody has checked it ("Har I handicaptoilet?").</summary>
public sealed record Facilitet(string Noegle, string Navn, string Spoergsmaal);

/// <summary>One need a person can tick in "Mit pas", and the facilities that need asks of a place.</summary>
/// <summary>TagMed = what to bring for this need (empty = nothing).</summary>
public sealed record Behov(string Noegle, string Gruppe, string Tekst, IReadOnlyList<string> Kraever, string TagMed = "");

/// <summary>
/// The fixed lists behind Easy-Pass. Groups follow Jacob's needs profile (bevægelse, syn, hørelse, skjulte handicap,
/// overblik, allergi) but in plain Danish "Jeg ..." sentences. A need with no facility (fx "Jeg ser dårligt" when no place
/// has said anything) shows as "Tjek selv", never as "Passer".
/// </summary>
public static class Katalog
{
    public static readonly IReadOnlyList<Facilitet> Faciliteter =
    [
        new("niveaufri", "Adgang uden trin (eller rampe)", "Kan man komme ind uden trin, eller er der en rampe?"),
        new("elevator", "Elevator til alle etager for gæster", "Er der elevator til alle etager, gæster må bruge?"),
        new("toilet", "Handicaptoilet", "Har I et handicaptoilet?"),
        new("parkering", "Handicapparkering tæt på indgangen", "Er der handicapparkering tæt på indgangen?"),
        new("siddepladser", "Steder at sidde og hvile", "Er der steder, hvor man kan sidde og hvile?"),
        new("korte-afstande", "Korte afstande inde på stedet", "Hvor langt skal man gå inde hos jer?"),
        new("stille-rum", "Et stille sted at trække sig", "Er der et stille sted, man kan trække sig til?"),
        new("roligt", "Normalt roligt (ikke larmende)", "Hvornår er der mest roligt hos jer?"),
        new("teleslynge", "Teleslynge", "Har I teleslynge?"),
        new("skrift", "Vigtig info på skrift (skilte, tekst)", "Står de vigtige ting på skilte eller på skrift?"),
        new("stor-skrift", "Stor skrift eller lyd/oplæsning", "Har I stor skrift eller oplæsning?"),
        new("ledsagerkort", "Ledsager gratis med ledsagerkort", "Kommer en ledsager gratis ind med ledsagerkort?"),
        new("hund", "Servicehunde er velkomne", "Må min servicehund komme med ind?"),
        new("solsikke", "Personalet kender solsikkesnoren", "Kender personalet solsikkesnoren?"),
        new("allergivenlig", "Allergivenligt (fx ingen dyr, røg eller stærk duft)", "Er der dyr, røg eller stærk duft hos jer?"),
    ];

    public static readonly IReadOnlyList<Behov> Alle =
    [
        new("korestol", "Bevægelse", "Jeg bruger kørestol", ["niveaufri", "elevator", "toilet"]),
        new("rollator", "Bevægelse", "Jeg bruger rollator eller stok", ["niveaufri", "siddepladser"]),
        new("trapper", "Bevægelse", "Jeg kan ikke gå på trapper", ["niveaufri", "elevator"]),
        new("arm-haand", "Bevægelse", "Jeg har svært ved at bruge arme eller hænder", []),
        new("parkering", "Bevægelse", "Jeg skal parkere tæt på", ["parkering"]),
        new("toilet", "Bevægelse", "Jeg har brug for handicaptoilet", ["toilet"]),

        new("kort-gang", "Energi", "Jeg kan kun gå kort", ["korte-afstande", "siddepladser"]),
        new("hvile", "Energi", "Jeg skal kunne sidde og hvile", ["siddepladser"]),

        new("syn", "Syn", "Jeg ser dårligt", ["stor-skrift"]),
        new("hoerelse", "Hørelse", "Jeg hører dårligt", ["teleslynge", "skrift"]),
        new("tegnsprog", "Hørelse", "Jeg taler tegnsprog eller skal have ting på skrift", ["skrift"]),

        new("larm", "Skjulte handicap", "Jeg har det svært med larm og mange mennesker", ["roligt", "stille-rum"], "Høretelefoner eller ørepropper"),
        new("angst", "Skjulte handicap", "Jeg kan få angst og skal kunne trække mig", ["stille-rum"], "Høretelefoner eller noget, der gør dig tryg"),
        new("solsikke", "Skjulte handicap", "Jeg bærer solsikkesnor (skjult handicap)", ["solsikke"], "Din solsikkesnor"),
        new("overblik", "Overblik", "Jeg har brug for ekstra tid og et klart overblik", ["skrift", "roligt"]),

        new("ledsager", "Andet", "Jeg har ledsagerkort", ["ledsagerkort"], "Dit ledsagerkort"),
        new("hund", "Andet", "Jeg har en servicehund", ["hund"], "Hundens dækken og papirer"),
        new("allergi", "Andet", "Jeg har allergi eller astma", ["allergivenlig"], "Din medicin"),
    ];

    public static readonly IReadOnlyList<string> Kategorier =
        ["Museum", "Kultur og teater", "Natur og park", "Spisested", "Overnatning", "Aktivitet", "Kirke", "Butik", "Andet"];

    private static readonly Dictionary<string, Behov> ByKey = Alle.ToDictionary(b => b.Noegle);
    private static readonly Dictionary<string, Facilitet> FacByKey = Faciliteter.ToDictionary(f => f.Noegle);

    public static Behov? FindBehov(string noegle) => ByKey.GetValueOrDefault(noegle);
    public static Facilitet? FindFacilitet(string noegle) => FacByKey.GetValueOrDefault(noegle);

    /// <summary>"korestol,larm,ukendt" -> the known needs only, each once, in catalogue order.</summary>
    public static IReadOnlyList<Behov> Laes(string? noegler)
    {
        var wanted = (noegler ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet();
        return Alle.Where(b => wanted.Contains(b.Noegle)).ToList();
    }
}

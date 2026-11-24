using System.Globalization;

namespace ITMartinElPriser.Domain;

// The advisor's visit (user 2026-11-24: "I visit or work remote, set things up for them and explain"). One checklist per
// customer household; most steps tick themselves from the household's own data, the rest Martin ticks by hand. When it is
// done the customer gets a handover sheet that says what was set up, how to use it and how to reach Martin.

/// <summary>What the household's data shows - gathered by the app, judged here.</summary>
public sealed record SetupFacts(
    string HouseholdName,
    string OwnerEmail,
    bool MeterConnected,
    string MeterAddress,
    DateOnly? ConsumptionUntil,
    string GridName,
    string PriceArea,
    ElBill? LatestBill,
    IReadOnlyList<string> Machines,
    int DeviceCount,
    int NotifyPhones,
    DateTime? AdvisorAccessUntilUtc);

public sealed record SetupStep(string Id, string Title, string How, Func<SetupFacts, bool>? Auto = null)
{
    public bool IsAuto => Auto is not null;
}

public sealed record SetupStepState(SetupStep Step, bool Done, DateTime? TickedUtc);

public static class CustomerSetup
{
    public static readonly CultureInfo Da = CultureInfo.GetCultureInfo("da-DK");

    public static readonly IReadOnlyList<SetupStep> Steps =
    [
        new("meter", "Elmåleren er forbundet",
            "Kunden logger ind på eloverblik.dk med MitID → Datadeling → Opret token → kopierer nøglen ind i MinElpris (forsiden spørger selv første gang).",
            f => f.MeterConnected),
        new("usage", "Forbruget er hentet",
            "Kommer af sig selv et år tilbage, når måleren er forbundet. Tag et halvt minut – kig så under Dag.",
            f => f.ConsumptionUntil is not null),
        new("grid", "Netselskab og prisområde er fundet",
            "Findes ud fra målerens adresse. Står der ikke noget, så tryk 📍 på forsiden.",
            f => f.GridName.Length > 0),
        new("bill", "Seneste elregning er lagt ind",
            "Under Regning → Upload regning: PDF'en fra elselskabets app eller mail (fx NRGi → Mine dokumenter). Så kender appen aftale, tillæg og abonnement.",
            f => f.LatestBill is not null),
        new("deal", "Elaftalen er sammenlignet med de billigste",
            "Under Elselskab: vis kunden 'din aftale mod de billigste lignende', og hvad et skifte giver om året. Kunden beslutter selv."),
        new("machines", "Maskinerne er sat op",
            "Under Enheder: vaskemaskine, opvask og tørretumbler med model (✨ Skøn forbrug). Så viser forsiden prisen pr. vask.",
            f => f.Machines.Count > 0),
        new("devices", "Strømslugerne er registreret",
            "Under Enheder: gå huset igennem sammen (køleskab, fryser, TV, varmepumpe, elbil …). Mindst de 3 største.",
            f => f.DeviceCount >= 3),
        new("notify", "Besked er slået til på kundens telefon",
            "Under Besked → Slå beskeder til → Send test. iPhone: først Del → Føj til hjemmeskærm og åbn appen derfra.",
            f => f.NotifyPhones > 0),
        new("show", "Kunden har set Nu, Dag og Regning",
            "Lad kunden selv trykke: Nu (hvornår er strømmen billig), Dag (hvad brugte vi i går), Regning (hvor går pengene hen)."),
        new("contact", "Kunden ved, hvordan de får fat i dig",
            "Telefon 31 19 47 30, mail ITMartin@Mensa.dk – og at du kan kigge med, så længe rådgiveradgangen er åben."),
        new("handover", "Overdragelsesarket er givet",
            "Print arket eller send det som PDF (Udskriv → Gem som PDF), og gå det igennem med kunden."),
    ];

    public static List<SetupStepState> Evaluate(SetupFacts facts, IReadOnlyDictionary<string, DateTime> ticks) =>
        [.. Steps.Select(s => s.Auto is { } auto
            ? new SetupStepState(s, auto(facts), null)
            : new SetupStepState(s, ticks.ContainsKey(s.Id), ticks.TryGetValue(s.Id, out var at) ? at : null))];

    public static (int Done, int Total) Progress(IEnumerable<SetupStepState> states)
    {
        var list = states.ToList();
        return (list.Count(s => s.Done), list.Count);
    }

    /// <summary>The handover sheet's "Det har vi sat op" lines - only what is really there, in plain Danish.</summary>
    public static List<(string Label, string Value)> WhatWasSetUp(SetupFacts f)
    {
        var lines = new List<(string, string)>();
        if (f.MeterConnected)
            lines.Add(("Elmåler", f.MeterAddress.Length > 0 ? $"Forbundet ({f.MeterAddress})" : "Forbundet"));
        if (f.ConsumptionUntil is { } until)
            lines.Add(("Forbrug", $"Hentet til og med {until.ToString("d. MMMM yyyy", Da)} – det opdateres selv hver 6. time"));
        if (f.GridName.Length > 0)
            lines.Add(("Netselskab", $"{f.GridName} · prisområde {(f.PriceArea == "DK2" ? "DK2 (Østdanmark)" : "DK1 (Vestdanmark)")}"));
        if (f.LatestBill is { } b)
            lines.Add(("Elaftale", $"{b.Supplier} {b.Product}".Trim() + (b.To is { } to ? $" (regning til {to.ToString("d. MMM yyyy", Da)})" : "")));
        if (f.Machines.Count > 0)
            lines.Add(("Maskiner", string.Join(", ", f.Machines)));
        if (f.DeviceCount > 0)
            lines.Add(("Enheder", $"{f.DeviceCount} registreret"));
        if (f.NotifyPhones > 0)
            lines.Add(("Besked", f.NotifyPhones == 1 ? "Slået til på 1 telefon" : $"Slået til på {f.NotifyPhones} telefoner"));
        return lines;
    }
}

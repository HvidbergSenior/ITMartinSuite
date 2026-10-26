using ITMartinAdmin.Contracts;

namespace ITMartinAdmin.Domain;

// ITMartinAdmin (user 2026-10-26: "i need a ITMartinAdmin" -> "one front door"): one login and one page with buttons to
// every admin page, and a few live numbers from the apps.

/// <summary>One button on the front page: what it opens and what you do there.</summary>
public sealed record AdminLink(string Group, string Title, string Url, string What);

public static class AdminGroups
{
    public const string Bogshoppen = "Bogshoppen", ITMartin = "ITMartin", Drift = "Drift";
    public static readonly string[] Order = [Bogshoppen, ITMartin, Drift];
}

/// <summary>The admin pages, grouped (the order here is the order on the page).</summary>
public static class AdminLinks
{
    public static readonly IReadOnlyList<AdminLink> All =
    [
        new(AdminGroups.Bogshoppen, "Sessioner", "https://vareinfo.bogshoppen.dk/sessioner", "Gør kasser klar til hjælperne, giv dem navne og print labels."),
        new(AdminGroups.Bogshoppen, "Hjælpere", "https://vareinfo.bogshoppen.dk/hjaelpere", "Hvem tager billeder lige nu, og hjælpernes koder."),
        new(AdminGroups.Bogshoppen, "Kasser", "https://lager.bogshoppen.dk/kasser", "Hvad er der i kasserne – læs billederne, se hvad der er fundet."),
        new(AdminGroups.Bogshoppen, "Søg i lageret", "https://lager.bogshoppen.dk/", "Find en ting, ret pris og placering."),
        new(AdminGroups.Bogshoppen, "Ordrer", "https://salg.bogshoppen.dk/", "Det, der er købt: pak det og send det."),
        new(AdminGroups.Bogshoppen, "Interesse", "https://salg.bogshoppen.dk/interesse", "Det, kunderne har markeret som interessant – til liveauktionerne."),
        new(AdminGroups.Bogshoppen, "Tilbud", "https://salg.bogshoppen.dk/tilbud", "Dagens tilbud i butikken."),
        new(AdminGroups.ITMartin, "Svar", "https://itmartin.dk/admin", "Chatten, spørgsmål og siderne på itmartin.dk og bogshoppen.dk."),
        new(AdminGroups.ITMartin, "Tal", "https://itmartin.dk/admin/tal", "Hvor mange der besøger siderne og apps."),
        new(AdminGroups.ITMartin, "Kunder", "https://kunder.itmartin.dk/", "Kundekartoteket: alle tilmeldte, hjælpere og de gratis apps."),
        new(AdminGroups.ITMartin, "Skriv til kunderne", "https://kunder.itmartin.dk/skriv", "Mails til kunderne."),
        new(AdminGroups.ITMartin, "Stem", "https://stem.itmartin.dk/admin", "Afstemninger og datoer."),
        new(AdminGroups.Drift, "Kontrol", "https://kontrol.itmartin.dk/", "Start og stop apps, og se om serverne har det godt."),
    ];

    public static IEnumerable<IGrouping<string, AdminLink>> Grouped() =>
        All.GroupBy(l => l.Group).OrderBy(g => Array.IndexOf(AdminGroups.Order, g.Key));
}

/// <summary>What an app answered - or that it did not.</summary>
public sealed record AppAnswer(string App, List<StatusLine> Lines, bool Answered)
{
    public static AppAnswer Down(string app) => new(app, [], false);
}

public static class Dashboard
{
    /// <summary>The "do something" lines from every app, first on the page - only those with a count.</summary>
    public static List<(string App, StatusLine Line)> ToDo(IEnumerable<AppAnswer> answers) =>
        answers.SelectMany(a => a.Lines.Where(l => l.Attention && l.Count > 0).Select(l => (a.App, l))).ToList();

    /// <summary>"Alt er roligt" when nothing needs doing and every app answered.</summary>
    public static bool AllQuiet(IReadOnlyCollection<AppAnswer> answers) => answers.All(a => a.Answered) && ToDo(answers).Count == 0;
}

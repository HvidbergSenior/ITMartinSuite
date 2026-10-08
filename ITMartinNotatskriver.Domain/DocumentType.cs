namespace ITMartinNotatskriver.Domain;

/// <summary>A kind of document the notes can become, with the instruction the AI writes it from.</summary>
public sealed record DocumentType(string Key, string Icon, string Title, string Instruction)
{
    public static readonly IReadOnlyList<DocumentType> All =
    [
        new("referat", "🗓", "Mødereferat", "Et mødereferat: overskrift med møde og dato hvis kendt, deltagere hvis nævnt, derefter punkterne drøftet, beslutninger og en liste 'Opgaver' med hvem/hvad/hvornår, når noterne siger det."),
        new("brev", "✉", "Brev", "Et brev med hilsen, tydelig besked i korte afsnit og afslutning. Afsender/modtager kun hvis noterne nævner dem."),
        new("mail", "📧", "Mail", "En kort mail: forslag til emnelinje først ('Emne: ...'), derefter mailen. Venlig og til sagen."),
        new("rapport", "📊", "Rapport", "En rapport med overskrifter: Baggrund, Hvad vi fandt, Konklusion, Næste skridt - kun de afsnit noterne giver indhold til."),
        new("ansoegning", "📝", "Ansøgning", "En ansøgning: hvad der søges, hvorfor, hvad ansøgeren kan/har gjort. Personlig men saglig."),
        new("opslag", "📣", "Opslag", "Et opslag til Facebook/LinkedIn: fanger i første linje, kort, letlæseligt, højst 3 emojis, evt. 2-3 hashtags til sidst."),
        new("telefon", "📞", "Telefonnotat", "Et telefonnotat: hvem, hvornår (hvis nævnt), hvad blev sagt, hvad blev aftalt."),
        new("plan", "✅", "Plan / huskeliste", "En overskuelig plan: grupperede punkter i rækkefølge, med datoer/ansvarlige hvis noterne nævner dem."),
        new("resume", "📄", "Resumé", "Et kort resumé på 3-6 sætninger med det vigtigste først."),
        new("renskriv", "🧾", "Renskriv mine noter", "Noterne renskrevet: samme indhold og rækkefølge, men hele sætninger, rettet stavning og ryddelig opsætning."),
    ];

    public static DocumentType Default => All[0];
}

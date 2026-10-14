namespace ITMartinSkrivSange.Domain;

/// <param name="Home">The chord it comes home to (the key), as an open grip.</param>
/// <param name="Feel">One line on how it sounds, shown under the chords.</param>
public sealed record Progression(string Id, string Mood, string Name, IReadOnlyList<string> Chords, string Home, string Feel)
{
    public bool Minor => Domain.Chords.IsMinor(Home);
    public string Family => Domain.Chords.MajorFamily(Home);
}

// The progressions to pick from, by mood. All in open grips (see Chords.OpenGrips) - the capo changes the key.
public static class ProgressionLibrary
{
    public static readonly IReadOnlyList<string> Moods = ["Glad", "Rolig", "Melankolsk", "Trist", "Dramatisk", "Blues"];

    public static readonly IReadOnlyList<Progression> All =
    [
        new("glad-pop",    "Glad", "Popklassikeren", ["G", "D", "Em", "C"], "G", "Lys og løftet - brugt i hundredvis af hits"),
        new("glad-lejr",   "Glad", "Lejrbål",        ["C", "G", "Am", "Fmaj7"], "C", "Varm og åben, Fmaj7 er uden barré"),
        new("glad-folk",   "Glad", "Folkemelodi",    ["G", "C", "G", "D"], "G", "Enkel og munter, let at synge til"),
        new("glad-sol",    "Glad", "Solskin",        ["D", "G", "D", "A"], "D", "Lys og ligetil"),

        new("rolig-50",    "Rolig", "Halvtredser-ballade", ["C", "Am", "Fmaj7", "G"], "C", "Blid og tryg"),
        new("rolig-vugge", "Rolig", "Vuggevise",           ["G", "Em", "C", "D"], "G", "Rolig og rund"),
        new("rolig-luft",  "Rolig", "Luftig",              ["G", "Cadd9", "Em", "Dsus4"], "G", "De åbne strenge klinger med"),
        new("rolig-morgen","Rolig", "Stille morgen",       ["D", "Asus2", "G", "D"], "D", "Bløde, åbne greb"),

        new("mel-laengsel","Melankolsk", "Længsel",   ["Em", "C", "G", "D"], "Em", "Popklassikeren, startet i mol"),
        new("mel-bitter",  "Melankolsk", "Bittersød", ["Am", "Fmaj7", "C", "G"], "Am", "Eftertænksom, men ikke tung"),
        new("mel-droem",   "Melankolsk", "Drømmende", ["Em", "Cadd9", "G", "Dsus4"], "Em", "Åbne strenge og lidt tåge"),
        new("mel-regn",    "Melankolsk", "Regnvejr",  ["Am", "Em", "Fmaj7", "G"], "Am", "Stille sorg"),

        new("trist-uro",   "Trist", "Uro",            ["Am", "Dm", "Am", "E"], "Am", "Trist med et stik af uro i E"),
        new("trist-mol",   "Trist", "Mol hele vejen", ["Em", "Am", "Em", "B7"], "Em", "Sørgmodig, B7 trækker hjem"),
        new("trist-tung",  "Trist", "Tung",           ["Dm", "Am", "Dm", "A7"], "Dm", "Mørk og alvorlig"),
        new("trist-vemod", "Trist", "Vemod",          ["Am", "Dm", "G", "Am"], "Am", "En vemodig fortælling"),

        new("dram-trappe", "Dramatisk", "Spansk trappe",  ["Am", "G", "Fmaj7", "E"], "Am", "Går ned ad trappen og ender spændt på E"),
        new("dram-stor",   "Dramatisk", "Stort og mørkt", ["Em", "D", "C", "B7"], "Em", "Samme trappe i E - stor lyd"),
        new("dram-fort",   "Dramatisk", "Fortælling",     ["Am", "C", "D", "Fmaj7"], "Am", "Bygger op linje for linje"),
        new("dram-bakke",  "Dramatisk", "Op ad bakke",    ["C", "D", "Em", "Em"], "Em", "Klatrer op og lander i mol"),

        new("blues-a",     "Blues", "Blues i A", ["A7", "D7", "A7", "E7"], "A7", "Lige ud ad landevejen"),
        new("blues-e",     "Blues", "Blues i E", ["E", "A", "E", "B7"], "E", "Rå og jordnær"),
        new("blues-g",     "Blues", "Country",   ["G", "C", "G", "D7"], "G", "Glad blues"),
        new("blues-shuf",  "Blues", "Shuffle",   ["E7", "A7", "B7", "A7"], "E7", "Driver fremad"),
    ];

    public static Progression? Find(string? id) => id is null ? null : All.FirstOrDefault(p => p.Id == id);

    public static IReadOnlyList<Progression> ForMood(string mood) => All.Where(p => p.Mood == mood).ToList();
}

/// <param name="Pattern">One bar of 4/4 in eighths: D = down, U = up, - = let it ring.</param>
public sealed record StrumPattern(string Id, string Name, string Pattern, string Feel);

// The strums the page can play (Web Audio in the browser, no sound files).
public static class Strums
{
    public static readonly IReadOnlyList<StrumPattern> All =
    [
        new("rolig", "Rolig", "D---D---", "Et slag på 1 og 3 - til ballader og intro"),
        new("pop",   "Pop",   "D-DU-UDU", "Den mest brugte - passer til næsten alt"),
        new("folk",  "Folk",  "D-D-DUDU", "Fremad uden at larme"),
        new("rock",  "Rock",  "DDDDDDDD", "Ottendedele ned - kraftigt"),
    ];

    public static StrumPattern Default => All[1];

    public static StrumPattern Find(string? id) => All.FirstOrDefault(s => s.Id == id) ?? Default;
}

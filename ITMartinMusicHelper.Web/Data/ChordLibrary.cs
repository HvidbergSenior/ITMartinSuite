namespace ITMartinMusicHelper.Web.Data;

// Strings: 6 values low-E→high-e. -1=muted, 0=open, >=1=absolute fret number.
// StartFret: which fret sits at the top of the diagram (1 = nut position).
public record ChordVoicing(
    string Label,
    int StartFret,
    int[] Strings,
    string? Tip = null
);

public static class ChordLibrary
{
    public static readonly Dictionary<string, ChordVoicing[]> Voicings = new()
    {
        ["G"] =
        [
            new("G basic",    1, [3, 2, 0, 0, 0, 3],  "Low E fret 3 + A fret 2 — lad de andre strenge klinge"),
            new("G full",     1, [3, 2, 0, 0, 3, 3],  "Ring- og lillefinger sammen på B og e — fyldigere klang"),
            new("G7",         1, [3, 2, 0, 0, 0, 1],  "G7 — skifter til C bagefter? Perfekt"),
            new("Gsus2",      1, [3, 0, 0, 0, 3, 3],  "Gsus2 — løftet og åben lyd"),
            new("G let",      1, [-1, -1, 0, 0, 3, 3], "Kun 4 strenge — nemmeste G til begyndere"),
        ],
        ["C"] =
        [
            new("C standard", 1, [-1, 3, 2, 0, 1, 0], "A fret 3 + D fret 2 + B fret 1 — klassisk C"),
            new("C let",      1, [-1, -1, 2, 0, 1, 0],"Kun 4 strenge — god til begyndere"),
            new("Cmaj7",      1, [-1, 3, 2, 0, 0, 0], "Cmaj7 — blød og drømmende, en finger færre"),
            new("C/G",        1, [3, 3, 2, 0, 1, 0],  "C med G i bassen — lyder meget rigtigt"),
        ],
        ["D"] =
        [
            new("D standard", 1, [-1, -1, 0, 2, 3, 2],"Kun de tre tynde strenge — let at lære"),
            new("Dsus2",      1, [-1, -1, 0, 2, 3, 0],"Dsus2 — løftet og åben"),
            new("Dsus4",      1, [-1, -1, 0, 2, 3, 3],"Dsus4 — glid ind i D fra denne"),
            new("D/F#",       1, [2, 0, 0, 2, 3, 2],  "D med F# i bassen — flot ganglinie"),
        ],
        ["Em"] =
        [
            new("Em åben",    1, [0, 2, 2, 0, 0, 0],  "Nemmeste akkord — kun A og D på fret 2"),
            new("Em7",        1, [0, 2, 0, 0, 0, 0],  "Em7 — én finger! Meget let"),
            new("Em fuld",    1, [0, 2, 2, 0, 0, 0],  "Alle seks strenge klingende"),
            new("Em9",        1, [0, 2, 2, 0, 3, 0],  "Em9 — luftig og melankolsk"),
        ],
        ["Am"] =
        [
            new("Am standard",1, [-1, 0, 2, 2, 1, 0], "A open + tre fingre — meget brugt akkord"),
            new("Am7",        1, [-1, 0, 2, 0, 1, 0], "Am7 — løsere og jazzy, to fingre"),
            new("Am let",     1, [-1, 0, 2, 2, 0, 0], "To fingre — nemt for begyndere"),
            new("Asus2",      1, [-1, 0, 2, 2, 0, 0], "Asus2 — åben og smukt"),
        ],
        ["F"] =
        [
            new("Fmaj7",      1, [-1, -1, 3, 2, 1, 0],"Fmaj7 — nemmere end F, lyder dejligt"),
            new("F mini",     1, [-1, -1, 3, 2, 1, 1],"Mini-barre på B og e — god mellemvej"),
            new("F barre",    1, [1, 1, 2, 3, 3, 1],  "Fuld barre fret 1 — kræver øvelse"),
            new("Fadd9",      1, [-1, -1, 3, 0, 1, 1],"Fadd9 — alternativ til barre-F"),
        ],
        ["E"] =
        [
            new("E standard", 1, [0, 2, 2, 1, 0, 0],  "Kraftfuld åben E — alle seks strenge"),
            new("E7",         1, [0, 2, 0, 1, 0, 0],  "E7 — bluesagtig, nem overgang fra E"),
            new("Emaj7",      1, [0, 2, 1, 1, 0, 0],  "Emaj7 — elegant og rolig"),
        ],
        ["A"] =
        [
            new("A standard", 1, [-1, 0, 2, 2, 2, 0], "Tre fingre på fret 2 — åben A"),
            new("A7",         1, [-1, 0, 2, 0, 2, 0], "A7 — to fingre, bluesrock"),
            new("Asus4",      1, [-1, 0, 2, 2, 3, 0], "Asus4 — spænding inden A"),
        ],
        ["Dm"] =
        [
            new("Dm standard",1, [-1, -1, 0, 2, 3, 1],"Klassisk Dm — melankolsk og smuk"),
            new("Dm7",        1, [-1, -1, 0, 2, 1, 1],"Dm7 — blødere og jazzet"),
            new("Dmadd9",     1, [-1, -1, 0, 2, 3, 0],"Dmadd9 — åbent og smukt"),
        ],
        ["Bm"] =
        [
            new("Bm let",     2, [-1, -1, 4, 4, 3, 2],"Fire strenge ved 2. bånd — nemmere end barre"),
            new("Bm barre",   2, [-1, 2, 4, 4, 3, 2], "Barre ved 2. bånd — fuld og kraftfuld lyd"),
            new("Bm7",        1, [-1, 2, 0, 2, 0, 2], "Bm7 åben form — nemmere alternativ"),
        ],
    };

    public static ChordVoicing[] GetVoicings(string chord) =>
        Voicings.TryGetValue(chord, out var v) ? v : [];
}

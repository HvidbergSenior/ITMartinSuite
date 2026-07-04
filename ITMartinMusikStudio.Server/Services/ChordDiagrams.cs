namespace ITMartinMusikStudio.Server.Services;

public static class ChordDiagrams
{
    // 6 strings low→high: E A D G B e
    // -1 = muted (×), 0 = open (○), N = fret number
    private static readonly Dictionary<string, int[]> ExactShapes = new(StringComparer.OrdinalIgnoreCase)
    {
        { "A",     [-1, 0, 2, 2, 2, 0] },
        { "A7",    [-1, 0, 2, 0, 2, 0] },
        { "Am",    [-1, 0, 2, 2, 1, 0] },
        { "Am7",   [-1, 0, 2, 0, 1, 0] },
        { "Amaj7", [-1, 0, 2, 1, 2, 0] },
        { "Asus2", [-1, 0, 2, 2, 0, 0] },
        { "Asus4", [-1, 0, 2, 2, 3, 0] },
        { "B",     [-1, 2, 4, 4, 4, 2] },
        { "Bm",    [-1, 2, 4, 4, 3, 2] },
        { "B7",    [-1, 2, 1, 2, 0, 2] },
        { "Bb",    [ 1, 1, 3, 3, 3, 1] },
        { "Bbm",   [ 1, 1, 3, 3, 2, 1] },
        { "C",     [-1, 3, 2, 0, 1, 0] },
        { "Cm",    [-1, 3, 5, 5, 4, 3] },
        { "C7",    [-1, 3, 2, 3, 1, 0] },
        { "Cadd9", [-1, 3, 2, 0, 3, 3] },
        { "Cmaj7", [-1, 3, 2, 0, 0, 0] },
        { "D",     [-1,-1, 0, 2, 3, 2] },
        { "Dm",    [-1,-1, 0, 2, 3, 1] },
        { "D7",    [-1,-1, 0, 2, 1, 2] },
        { "Dmaj7", [-1,-1, 0, 2, 2, 2] },
        { "Dsus2", [-1,-1, 0, 2, 3, 0] },
        { "Dsus4", [-1,-1, 0, 2, 3, 3] },
        { "E",     [ 0, 2, 2, 1, 0, 0] },
        { "Em",    [ 0, 2, 2, 0, 0, 0] },
        { "E7",    [ 0, 2, 0, 1, 0, 0] },
        { "Emaj7", [ 0, 2, 1, 1, 0, 0] },
        { "F",     [ 1, 1, 2, 3, 3, 1] },
        { "Fm",    [ 1, 1, 3, 3, 2, 1] },
        { "F7",    [ 1, 1, 2, 1, 3, 1] },
        { "Fmaj7", [-1,-1, 3, 2, 2, 1] },
        { "F#m",   [ 2, 4, 4, 2, 2, 2] },
        { "Gbm",   [ 2, 4, 4, 2, 2, 2] },
        { "G",     [ 3, 2, 0, 0, 0, 3] },
        { "Gm",    [ 3, 5, 5, 3, 3, 3] },
        { "G7",    [ 3, 2, 0, 0, 0, 1] },
        { "Gmaj7", [ 3, 2, 0, 0, 0, 2] },
        { "Gsus4", [ 3, 3, 0, 0, 1, 3] },
        { "C#m",   [-1, 4, 6, 6, 5, 4] },
        { "Dbm",   [-1, 4, 6, 6, 5, 4] },
        { "Db",    [-1, 4, 6, 6, 6, 4] },
        { "C#",    [-1, 4, 6, 6, 6, 4] },
        { "Ab",    [ 4, 6, 6, 5, 4, 4] },
        { "Abm",   [ 4, 6, 6, 4, 4, 4] },
        { "G#m",   [ 4, 6, 6, 4, 4, 4] },
        { "Eb",    [ 6, 6, 8, 8, 8, 6] },
        { "Ebm",   [ 6, 6, 8, 8, 7, 6] },
        { "D#m",   [ 6, 6, 8, 8, 7, 6] },
        { "A#m",   [ 6, 6, 8, 8, 7, 6] },
    };

    // Semitone distances from open E string (low E = 0)
    private static readonly string[] ChromaticFromE = ["E","F","F#","G","G#","A","A#","B","C","C#","D","D#"];
    private static readonly Dictionary<string, int> NoteToSemitone;
    static ChordDiagrams()
    {
        NoteToSemitone = new(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < ChromaticFromE.Length; i++)
            NoteToSemitone[ChromaticFromE[i]] = i;
        // Flat aliases
        NoteToSemitone["Db"] = 9;  // = C#
        NoteToSemitone["Eb"] = 11; // = D#
        NoteToSemitone["Gb"] = 6;  // = F#
        NoteToSemitone["Ab"] = 4;  // = G#
        NoteToSemitone["Bb"] = 6;  // = A#
    }

    public record DiagramResult(string Svg, string ShapeLabel);

    public static DiagramResult? Get(string? chordName)
    {
        if (string.IsNullOrEmpty(chordName)) return null;

        // 1. Try exact known shape
        if (ExactShapes.TryGetValue(chordName, out var exactFrets))
            return new DiagramResult(RenderSvg(exactFrets), "");

        // 2. Derive moveable barre chord
        var derived = DeriveMoveable(chordName);
        if (derived is null) return null;
        return new DiagramResult(RenderSvg(derived.Value.Frets), derived.Value.Label);
    }

    private static (int[] Frets, string Label)? DeriveMoveable(string chordName)
    {
        // Parse root (1 or 2 chars) and quality
        string root, quality;
        if (chordName.Length >= 2 && chordName[1] is '#' or 'b')
        { root = chordName[..2]; quality = chordName[2..]; }
        else
        { root = chordName[..1]; quality = chordName[1..]; }

        if (!NoteToSemitone.TryGetValue(root, out int rootSt)) return null;

        // Is it minor?
        bool isMinor = quality.StartsWith('m') && !quality.StartsWith("maj", StringComparison.OrdinalIgnoreCase);

        // E string is semitone 0; A string is semitone 5
        int eFret = rootSt;                   // E-form: barre this fret
        int aFret = ((rootSt - 5) + 12) % 12; // A-form: barre this fret

        if (isMinor)
        {
            // Em-form at eFret vs Am-form at aFret — pick lower
            // Em-shape: [B, B+2, B+2, B, B, B] — barre all 6 + two dots
            // Am-shape: [×, B, B+2, B+2, B+1, B]
            bool eOk = eFret is >= 1 and <= 9;
            bool aOk = aFret is >= 1 and <= 9;

            if (aOk && (!eOk || aFret <= eFret))
            {
                int b = aFret;
                return ([-1, b, b+2, b+2, b+1, b],
                    $"Am-form · barre fret {b} · samme greb som Am");
            }
            if (eOk)
            {
                int b = eFret;
                return ([b, b+2, b+2, b, b, b],
                    $"Em-form · barre fret {b} · samme greb som Em");
            }
        }
        else
        {
            // E-form or A-form — pick lower
            bool eOk = eFret is >= 1 and <= 9;
            bool aOk = aFret is >= 1 and <= 9;

            if (aOk && (!eOk || aFret <= eFret))
            {
                int b = aFret;
                return ([-1, b, b+2, b+2, b+2, b],
                    $"A-form · barre fret {b} · samme greb som A");
            }
            if (eOk)
            {
                int b = eFret;
                return ([b, b, b+1, b+2, b+2, b],
                    $"E-form · barre fret {b} · samme greb som E");
            }
        }
        return null;
    }

    private static string RenderSvg(int[] frets)
    {
        const int w = 80;
        const int h = 105;
        const int padLeft = 12;
        const int stringSpacing = 11;
        const int nutY = 28;
        const int fretHeight = 15;
        const int dotR = 5;
        const int numFrets = 4;

        var sx = Enumerable.Range(0, 6).Select(i => padLeft + i * stringSpacing).ToArray();
        var bottomY = nutY + numFrets * fretHeight;

        int minFret = frets.Where(f => f > 0).DefaultIfEmpty(0).Min();
        int firstFret = minFret <= 4 ? 1 : minFret;
        bool showFretLabel = firstFret > 1;

        var sb = new System.Text.StringBuilder();
        sb.Append($"<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 {w} {h}' width='{w}' height='{h}'>");

        // String lines
        for (int i = 0; i < 6; i++)
            sb.Append($"<line x1='{sx[i]}' y1='{nutY}' x2='{sx[i]}' y2='{bottomY}' stroke='#555' stroke-width='1.2'/>");

        // Nut or thin top + fret label
        if (!showFretLabel)
            sb.Append($"<line x1='{sx[0]}' y1='{nutY}' x2='{sx[5]}' y2='{nutY}' stroke='#ccc' stroke-width='3'/>");
        else
        {
            sb.Append($"<line x1='{sx[0]}' y1='{nutY}' x2='{sx[5]}' y2='{nutY}' stroke='#555' stroke-width='1'/>");
            sb.Append($"<text x='{sx[5] + 6}' y='{nutY + 4}' fill='#aaa' font-size='8' font-family='sans-serif'>{firstFret}fr</text>");
        }

        // Fret lines
        for (int f = 1; f <= numFrets; f++)
        {
            int fy = nutY + f * fretHeight;
            sb.Append($"<line x1='{sx[0]}' y1='{fy}' x2='{sx[5]}' y2='{fy}' stroke='#444' stroke-width='1'/>");
        }

        // Open (○) / muted (×) markers above nut
        for (int i = 0; i < 6; i++)
        {
            if (frets[i] == -1)
                sb.Append($"<text x='{sx[i]}' y='18' text-anchor='middle' fill='#666' font-size='11' font-family='monospace'>×</text>");
            else if (frets[i] == 0)
                sb.Append($"<circle cx='{sx[i]}' cy='16' r='4' fill='none' stroke='#888' stroke-width='1.5'/>");
        }

        // Barre detection: same fret on 4+ strings spanning 4+ string positions
        int barreAtFret = -1, barreLeft = -1, barreRight = -1;
        for (int testFret = firstFret; testFret <= firstFret + numFrets; testFret++)
        {
            int first = -1, last = -1, count = 0;
            for (int i = 0; i < 6; i++)
            {
                if (frets[i] == testFret) { if (first < 0) first = i; last = i; count++; }
            }
            if (count >= 4 && last - first >= 3)
            {
                barreAtFret = testFret; barreLeft = first; barreRight = last;
                break;
            }
        }

        // Draw barre bar
        if (barreAtFret > 0)
        {
            int relFret = barreAtFret - firstFret + 1;
            int cy = nutY + (relFret - 1) * fretHeight + fretHeight / 2;
            sb.Append($"<rect x='{sx[barreLeft] - dotR}' y='{cy - dotR}' width='{sx[barreRight] - sx[barreLeft] + 2 * dotR}' height='{2 * dotR}' rx='{dotR}' fill='white'/>");
        }

        // Individual finger dots
        for (int i = 0; i < 6; i++)
        {
            int f = frets[i];
            if (f <= 0) continue;
            if (f == barreAtFret && i >= barreLeft && i <= barreRight) continue;
            int relFret = f - firstFret + 1;
            if (relFret < 1 || relFret > numFrets) continue;
            int cy = nutY + (relFret - 1) * fretHeight + fretHeight / 2;
            sb.Append($"<circle cx='{sx[i]}' cy='{cy}' r='{dotR}' fill='white'/>");
        }

        sb.Append("</svg>");
        return sb.ToString();
    }

    // Legacy compat — kept so callers that only need the SVG still work
    public static string? GetSvg(string? chordName) => Get(chordName)?.Svg;
    public static bool HasDiagram(string? chordName) => Get(chordName) is not null;
}

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

    public static DiagramResult? Get(string? chordName) => GetAll(chordName).FirstOrDefault();

    // Up to 3 real, distinct ways to play the chord: the open/exact shape
    // (easiest, if one is known) plus the two moveable barre forms (E-form
    // and A-form) computed from music theory for ANY chord name - not just a
    // hand-curated list. Ordered easiest-first: open shape, then barre forms
    // by ascending fret (lower on the neck = smaller reach = "nicer").
    public static List<DiagramResult> GetAll(string? chordName)
    {
        if (string.IsNullOrEmpty(chordName)) return [];

        // Slash chord (e.g. "C/F") - there's no dedicated shape for the exact
        // bass note, so rather than silently picking one interpretation (or
        // dropping it), offer a real choice: the plain top chord's shapes,
        // plus the bass note's shapes clearly labelled - the player decides
        // which one matches how they want to voice it.
        var slashIdx = chordName.IndexOf('/');
        if (slashIdx > 0 && slashIdx < chordName.Length - 1)
        {
            var top = chordName[..slashIdx];
            var bass = chordName[(slashIdx + 1)..];
            var combined = new List<DiagramResult>();
            combined.AddRange(GetAllSingle(top).Take(2));
            combined.AddRange(GetAllSingle(bass).Take(2)
                .Select(d => new DiagramResult(d.Svg, $"Bas {bass}: {d.ShapeLabel}")));
            return combined;
        }

        return GetAllSingle(chordName);
    }

    private static List<DiagramResult> GetAllSingle(string chordName)
    {
        var results = new List<DiagramResult>();
        if (ExactShapes.TryGetValue(chordName, out var exactFrets))
            results.Add(new DiagramResult(RenderSvg(exactFrets), "Åben form · nemmest"));

        foreach (var (frets, label) in DeriveBothForms(chordName)
                     .OrderBy(f => f.Frets.Where(x => x > 0).DefaultIfEmpty(99).Min()))
            results.Add(new DiagramResult(RenderSvg(frets), label));

        return results.Take(3).ToList();
    }

    private static IEnumerable<(int[] Frets, string Label)> DeriveBothForms(string chordName)
    {
        // Parse root (1 or 2 chars) and quality
        string root, quality;
        if (chordName.Length >= 2 && chordName[1] is '#' or 'b')
        { root = chordName[..2]; quality = chordName[2..]; }
        else
        { root = chordName[..1]; quality = chordName[1..]; }

        if (!NoteToSemitone.TryGetValue(root, out int rootSt)) yield break;

        bool isMinor = quality.StartsWith('m') && !quality.StartsWith("maj", StringComparison.OrdinalIgnoreCase);

        // E string is semitone 0; A string is semitone 5. If the barre would
        // land on fret 0, that IS the plain open chord (already covered by
        // ExactShapes above) - shift an octave up so it's still a genuinely
        // distinct, real alternative rather than a duplicate of the open shape.
        int eFret = rootSt == 0 ? 12 : rootSt;
        int aFret = ((rootSt - 5) + 12) % 12;
        if (aFret == 0) aFret = 12;

        if (isMinor)
        {
            // Am-shape: [×, B, B+2, B+2, B+1, B] / Em-shape: [B, B+2, B+2, B, B, B]
            if (aFret is >= 1 and <= 9)
                yield return ([-1, aFret, aFret+2, aFret+2, aFret+1, aFret], $"Am-form · barre fret {aFret}");
            if (eFret is >= 1 and <= 9)
                yield return ([eFret, eFret+2, eFret+2, eFret, eFret, eFret], $"Em-form · barre fret {eFret}");
        }
        else
        {
            if (aFret is >= 1 and <= 9)
                yield return ([-1, aFret, aFret+2, aFret+2, aFret+2, aFret], $"A-form · barre fret {aFret}");
            if (eFret is >= 1 and <= 9)
                yield return ([eFret, eFret, eFret+1, eFret+2, eFret+2, eFret], $"E-form · barre fret {eFret}");
        }
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
        int maxFret = frets.Where(f => f > 0).DefaultIfEmpty(0).Max();
        // Only start the window at fret 1 if the WHOLE shape fits in it - a shape
        // whose lowest fret is small but that reaches higher (e.g. an A-form barre
        // at fret 3 spanning up to fret 5) must shift the window up, or the higher
        // frets fall outside the fixed 4-fret box and their dots silently vanish.
        int firstFret = maxFret <= numFrets ? 1 : minFret;
        bool showFretLabel = firstFret > 1;

        var sb = new System.Text.StringBuilder();
        sb.Append($"<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 {w} {h}' width='{w}' height='{h}'>");

        // Colors are CSS custom properties, not fixed hex, so the diagram
        // flips with the light/dark theme instead of e.g. staying a
        // hardcoded white dot that vanishes on a light card background.
        // String lines
        for (int i = 0; i < 6; i++)
            sb.Append($"<line x1='{sx[i]}' y1='{nutY}' x2='{sx[i]}' y2='{bottomY}' stroke='var(--chord-line)' stroke-width='1.2'/>");

        // Nut or thin top + fret label
        if (!showFretLabel)
            sb.Append($"<line x1='{sx[0]}' y1='{nutY}' x2='{sx[5]}' y2='{nutY}' stroke='var(--chord-nut)' stroke-width='3'/>");
        else
        {
            sb.Append($"<line x1='{sx[0]}' y1='{nutY}' x2='{sx[5]}' y2='{nutY}' stroke='var(--chord-line)' stroke-width='1'/>");
            sb.Append($"<text x='{sx[5] + 6}' y='{nutY + 4}' fill='var(--chord-label)' font-size='8' font-family='sans-serif'>{firstFret}fr</text>");
        }

        // Fret lines
        for (int f = 1; f <= numFrets; f++)
        {
            int fy = nutY + f * fretHeight;
            sb.Append($"<line x1='{sx[0]}' y1='{fy}' x2='{sx[5]}' y2='{fy}' stroke='var(--chord-fret)' stroke-width='1'/>");
        }

        // Open (○) / muted (×) markers above nut
        for (int i = 0; i < 6; i++)
        {
            if (frets[i] == -1)
                sb.Append($"<text x='{sx[i]}' y='18' text-anchor='middle' fill='var(--chord-muted)' font-size='11' font-family='monospace'>×</text>");
            else if (frets[i] == 0)
                sb.Append($"<circle cx='{sx[i]}' cy='16' r='4' fill='none' stroke='var(--chord-open)' stroke-width='1.5'/>");
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
            sb.Append($"<rect x='{sx[barreLeft] - dotR}' y='{cy - dotR}' width='{sx[barreRight] - sx[barreLeft] + 2 * dotR}' height='{2 * dotR}' rx='{dotR}' fill='var(--chord-dot)'/>");
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
            sb.Append($"<circle cx='{sx[i]}' cy='{cy}' r='{dotR}' fill='var(--chord-dot)'/>");
        }

        sb.Append("</svg>");
        return sb.ToString();
    }

    // Legacy compat — kept so callers that only need the SVG still work
    public static string? GetSvg(string? chordName) => Get(chordName)?.Svg;
    public static bool HasDiagram(string? chordName) => Get(chordName) is not null;
}

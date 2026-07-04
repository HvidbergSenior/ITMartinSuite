namespace ITMartinMusikStudio.Server.Services;

public static class ChordDiagrams
{
    // 6 strings low→high: E A D G B e
    // -1 = muted (×), 0 = open (○), N = fret number
    private static readonly Dictionary<string, int[]> Shapes = new(StringComparer.OrdinalIgnoreCase)
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
        { "G",     [ 3, 2, 0, 0, 0, 3] },
        { "Gm",    [ 3, 5, 5, 3, 3, 3] },
        { "G7",    [ 3, 2, 0, 0, 0, 1] },
        { "Gmaj7", [ 3, 2, 0, 0, 0, 2] },
        { "Gsus4", [ 3, 3, 0, 0, 1, 3] },
        { "C#m",   [-1, 4, 6, 6, 5, 4] },
        { "Db",    [-1, 4, 6, 6, 6, 4] },
    };

    public static string? GetSvg(string? chordName)
    {
        if (string.IsNullOrEmpty(chordName)) return null;
        if (!Shapes.TryGetValue(chordName, out var frets)) return null;

        const int w = 80;
        const int h = 105;
        const int padLeft = 12;
        const int stringSpacing = 11;
        const int nutY = 28;
        const int fretHeight = 15;
        const int dotR = 5;
        const int numFrets = 4;

        // Strings: string 0 = low E, string 5 = high e
        var sx = Enumerable.Range(0, 6).Select(i => padLeft + i * stringSpacing).ToArray();
        var bottomY = nutY + numFrets * fretHeight;

        // Determine first fret
        int minFret = frets.Where(f => f > 0).DefaultIfEmpty(0).Min();
        int firstFret = minFret <= 4 ? 1 : minFret;
        bool showFretLabel = firstFret > 1;

        var sb = new System.Text.StringBuilder();
        sb.Append($"<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 {w} {h}' width='{w}' height='{h}'>");

        // String lines
        for (int i = 0; i < 6; i++)
            sb.Append($"<line x1='{sx[i]}' y1='{nutY}' x2='{sx[i]}' y2='{bottomY}' stroke='#555' stroke-width='1.2'/>");

        // Nut (thick) or thin top + fret label
        if (!showFretLabel)
        {
            sb.Append($"<line x1='{sx[0]}' y1='{nutY}' x2='{sx[5]}' y2='{nutY}' stroke='#ccc' stroke-width='3'/>");
        }
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

        // Open/muted markers above nut
        for (int i = 0; i < 6; i++)
        {
            int f = frets[i];
            if (f == -1)
                sb.Append($"<text x='{sx[i]}' y='18' text-anchor='middle' fill='#666' font-size='11' font-family='monospace'>×</text>");
            else if (f == 0)
                sb.Append($"<circle cx='{sx[i]}' cy='16' r='4' fill='none' stroke='#888' stroke-width='1.5'/>");
        }

        // Detect barre: find fret value that appears on 3+ strings where firstFret context matches
        int barreAtFret = -1;
        int barreLeft = -1, barreRight = -1;
        for (int testFret = firstFret; testFret <= firstFret + numFrets; testFret++)
        {
            int first = -1, last = -1;
            int count = 0;
            for (int i = 0; i < 6; i++)
            {
                if (frets[i] == testFret) { if (first < 0) first = i; last = i; count++; }
            }
            if (count >= 4 && last - first >= 3) // at least 4 strings spanning 4+ positions
            {
                barreAtFret = testFret;
                barreLeft = first;
                barreRight = last;
                break;
            }
        }

        if (barreAtFret > 0)
        {
            int relFret = barreAtFret - firstFret + 1;
            int cy = nutY + (relFret - 1) * fretHeight + fretHeight / 2;
            sb.Append($"<rect x='{sx[barreLeft] - dotR}' y='{cy - dotR}' width='{sx[barreRight] - sx[barreLeft] + 2 * dotR}' height='{2 * dotR}' rx='{dotR}' fill='white'/>");
        }

        // Individual finger dots (skip barre fret strings covered by barre rect)
        for (int i = 0; i < 6; i++)
        {
            int f = frets[i];
            if (f <= 0) continue;
            if (f == barreAtFret && i >= barreLeft && i <= barreRight) continue; // covered by barre

            int relFret = f - firstFret + 1;
            if (relFret < 1 || relFret > numFrets) continue;
            int cy = nutY + (relFret - 1) * fretHeight + fretHeight / 2;
            sb.Append($"<circle cx='{sx[i]}' cy='{cy}' r='{dotR}' fill='white'/>");
        }

        sb.Append("</svg>");
        return sb.ToString();
    }

    public static bool HasDiagram(string? chordName) =>
        !string.IsNullOrEmpty(chordName) && Shapes.ContainsKey(chordName);
}

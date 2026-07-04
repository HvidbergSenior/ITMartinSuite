namespace ITMartinMusikStudio.Server.Services;

public static class PatternRenderer
{
    // ── Strumming ──────────────────────────────────────────────────────────────

    public static string RenderStrum(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";

        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        // Find a line that contains strum arrows; prefer SLAG: prefixed line
        string? arrowLine = null;
        string? beatLine  = null;

        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.StartsWith("SLAG:", StringComparison.OrdinalIgnoreCase))
            { arrowLine = line[5..].Trim(); continue; }
            if (line.StartsWith("TAK:", StringComparison.OrdinalIgnoreCase))
            { beatLine = line[4..].Trim(); continue; }

            if (arrowLine is null && (line.Contains('↓') || line.Contains('↑')))
                arrowLine = line;
            else if (beatLine is null && arrowLine is not null &&
                     line.Any(char.IsDigit) && !line.Contains('↓') && !line.Contains('↑'))
                beatLine = line;
        }

        if (arrowLine is null) return "";

        // Remove "/" separators and split into tokens
        var tokens = arrowLine.Replace("/", " ").Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
        var beats  = beatLine?.Replace("/", " ").Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);

        if (tokens.Length == 0) return "";

        var sb = new System.Text.StringBuilder();
        sb.Append("<div class='strum-grid'>");

        for (int i = 0; i < tokens.Length; i++)
        {
            var t    = tokens[i];
            var beat = beats is not null && i < beats.Length ? beats[i] : "";

            var (css, symbol) = t switch
            {
                "↓"          => ("strum-down",  "↓"),
                "↑"          => ("strum-up",    "↑"),
                "↓↑"         => ("strum-both",  "↓↑"),
                "–" or "-"   => ("strum-rest",  "–"),
                "×" or "x"   => ("strum-mute",  "×"),
                _            => ("strum-other", t)
            };

            sb.Append($"<div class='strum-cell {css}'>");
            sb.Append($"<span class='strum-arrow'>{symbol}</span>");
            if (!string.IsNullOrEmpty(beat))
                sb.Append($"<span class='strum-beat-num'>{beat}</span>");
            sb.Append("</div>");
        }

        sb.Append("</div>");
        return sb.ToString();
    }

    // ── Fingerpicking ──────────────────────────────────────────────────────────

    public static string RenderFingerpick(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";

        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        string? fingerLine = null;
        string? stringLine = null;

        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.StartsWith("FINGRE:", StringComparison.OrdinalIgnoreCase))
            { fingerLine = line[7..].Trim(); continue; }
            if (line.StartsWith("STRENGE:", StringComparison.OrdinalIgnoreCase))
            { stringLine = line[8..].Trim(); continue; }

            if (fingerLine is null && IsFingerLine(line))
                fingerLine = line;
            else if (stringLine is null && fingerLine is not null && IsStringLine(line))
                stringLine = line;
        }

        if (fingerLine is null) return "";

        var fingers = ParseFingerTokens(fingerLine);
        var strings = stringLine is not null
            ? ParseStringTokens(stringLine, fingers.Length)
            : DefaultStrings(fingers);

        if (fingers.Length == 0) return "";

        // Clamp strings array to same length
        if (strings.Length < fingers.Length)
        {
            var padded = new int[fingers.Length];
            Array.Copy(strings, padded, strings.Length);
            strings = padded;
        }

        string[] strNames = ["e", "B", "G", "D", "A", "E"];

        var sb = new System.Text.StringBuilder();
        sb.Append("<div class='pick-grid'>");

        // Header row with beat numbers
        sb.Append("<div class='pick-row'><div class='pick-label'></div>");
        for (int j = 0; j < fingers.Length; j++)
            sb.Append($"<div class='pick-cell pick-head'>{j + 1}</div>");
        sb.Append("</div>");

        // One row per string (string 1=high e on top, 6=low E on bottom)
        for (int s = 1; s <= 6; s++)
        {
            sb.Append("<div class='pick-row'>");
            sb.Append($"<div class='pick-label'>{strNames[s - 1]}</div>");

            for (int j = 0; j < fingers.Length; j++)
            {
                int str = j < strings.Length ? strings[j] : 0;
                string finger = fingers[j];

                if (str == s)
                {
                    var css = finger.ToLower() switch
                    {
                        "t" => "pick-thumb",
                        "i" => "pick-index",
                        "m" => "pick-mid",
                        "a" => "pick-ring",
                        _   => "pick-other"
                    };
                    sb.Append($"<div class='pick-cell {css}'>{finger.ToUpper()[0]}</div>");
                }
                else
                {
                    sb.Append("<div class='pick-cell pick-empty'>·</div>");
                }
            }

            sb.Append("</div>");
        }

        // Finger legend
        sb.Append("<div class='pick-legend'>");
        sb.Append("<span class='pick-thumb'>T</span>=tommelfinger &nbsp;");
        sb.Append("<span class='pick-index'>i</span>=pegefinger &nbsp;");
        sb.Append("<span class='pick-mid'>m</span>=langfinger &nbsp;");
        sb.Append("<span class='pick-ring'>a</span>=ringfinger");
        sb.Append("</div>");

        sb.Append("</div>");
        return sb.ToString();
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static bool IsFingerLine(string line)
    {
        var tokens = line.Split([' ', '\t', '/'], StringSplitOptions.RemoveEmptyEntries);
        int fingerCount = tokens.Count(t => System.Text.RegularExpressions.Regex.IsMatch(t, @"^[TimaTimA]$"));
        return fingerCount >= 2;
    }

    private static bool IsStringLine(string line)
    {
        var tokens = line.Split([' ', '\t', '/'], StringSplitOptions.RemoveEmptyEntries);
        int numCount = tokens.Count(t => int.TryParse(t, out int n) && n is >= 1 and <= 6);
        return numCount >= 2 && !line.Any(char.IsLetter);
    }

    private static string[] ParseFingerTokens(string line) =>
        line.Replace("/", " ").Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries)
            .Where(t => System.Text.RegularExpressions.Regex.IsMatch(t, @"^[TimaTimA]$"))
            .ToArray();

    private static int[] ParseStringTokens(string line, int max) =>
        line.Replace("/", " ").Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries)
            .Where(t => int.TryParse(t, out int n) && n is >= 1 and <= 6)
            .Select(int.Parse)
            .Take(max)
            .ToArray();

    private static int[] DefaultStrings(string[] fingers) =>
        fingers.Select(f => f.ToLower() switch
        {
            "t" => 6,
            "i" => 3,
            "m" => 2,
            "a" => 1,
            _   => 0
        }).ToArray();
}

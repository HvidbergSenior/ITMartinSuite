using System.Text.RegularExpressions;

namespace ITMartinMusic.Server.Services;

public sealed class ChordService
{
    private static readonly string[] Notes =
        ["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"];

    private static readonly Dictionary<string, string> FlatToSharp = new()
    {
        ["Db"] = "C#", ["Eb"] = "D#", ["Fb"] = "E", ["Gb"] = "F#",
        ["Ab"] = "G#", ["Bb"] = "A#", ["Cb"] = "B"
    };

    private static readonly Regex ChordRegex =
        new(@"^([A-G][b#]?)(m(?:aj)?[0-9]*|maj[0-9]*|[0-9]+|dim[0-9]*|aug[0-9]*|sus[24]?|add[0-9]+|m[0-9]*)?(/[A-G][b#]?)?$",
            RegexOptions.Compiled);

    public string TransposeChart(string chart, int semitones)
    {
        if (string.IsNullOrWhiteSpace(chart) || semitones == 0)
            return chart;

        return string.Join('\n',
            chart.Split('\n').Select(line => TransposeLine(line, semitones)));
    }

    public string TransposeKey(string key, int semitones)
    {
        if (string.IsNullOrWhiteSpace(key) || semitones == 0) return key;
        return TransposeChord(key, semitones);
    }

    private string TransposeLine(string line, int semitones)
    {
        var trimmed = line.Trim();
        if (trimmed.EndsWith(':'))
            return line;

        return Regex.Replace(line, @"[A-G][b#]?(?:m(?:aj)?[0-9]*|maj[0-9]*|[0-9]+|dim[0-9]*|aug[0-9]*|sus[24]?|add[0-9]+|m[0-9]*)?(?:/[A-G][b#]?)?(?=\s|$)",
            m => TransposeChord(m.Value, semitones));
    }

    private string TransposeChord(string chord, int semitones)
    {
        var match = ChordRegex.Match(chord);
        if (!match.Success) return chord;

        var root    = match.Groups[1].Value;
        var quality = match.Groups[2].Value;
        var bass    = match.Groups[3].Value;

        var newRoot = ShiftNote(root, semitones);
        var newBass = bass.Length > 1
            ? "/" + ShiftNote(bass[1..], semitones)
            : string.Empty;

        return newRoot + quality + newBass;
    }

    private string ShiftNote(string note, int semitones)
    {
        if (FlatToSharp.TryGetValue(note, out var sharp))
            note = sharp;

        var idx = Array.IndexOf(Notes, note);
        if (idx == -1) return note;

        return Notes[((idx + semitones) % 12 + 12) % 12];
    }

    public static bool IsChordToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return false;
        return ChordRegex.IsMatch(token.Trim());
    }
}

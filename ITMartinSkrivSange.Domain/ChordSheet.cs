using System.Text;
using System.Text.RegularExpressions;

namespace ITMartinSkrivSange.Domain;

// Text with chords over it (user 2026-10-29: "i need to have the text with those chords" while recording). Martin pastes
// the chord sheet himself (copied from Ultimate Guitar or typed) - the app never fetches or writes lyrics. The chord lines
// are moved to the song's grips, keeping each chord over the same word; the text lines stay as pasted.
public enum SheetLineKind { Blank, Section, Chords, Text }

public sealed record SheetLine(SheetLineKind Kind, string Text);

public static partial class ChordSheet
{
    public const int MaxLength = 20000;

    // A chord as a sheet writes it: root, then anything a chord name may hold (m7, sus4, add9, 7b9, +, dim, maj7 ...),
    // and an optional bass after a slash.
    [GeneratedRegex(@"^[A-G][#b]?(?:maj|min|m|dim|aug|sus|add|M|\+|°|ø|[0-9]|[#b](?=[0-9])|\(|\))*(?:/[A-G][#b]?)?$")]
    private static partial Regex ChordToken();

    // Things on a chord line that are not chords: bars, repeats, "N.C.", dashes.
    [GeneratedRegex(@"^(?:\||\|\||-+|x\d+|\(x\d+\)|\d+x|N\.?C\.?|\*+|\.+|/)$", RegexOptions.IgnoreCase)]
    private static partial Regex Filler();

    [GeneratedRegex(@"^\s*\[[^\]]+\]\s*$|^\s*(?:intro|vers|verse|omkvæd|chorus|pre-chorus|bro|bridge|outro|solo|interlude|instrumental|refrain|coda)\b[^a-zæøå]*\d*\s*:?\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex SectionLine();

    public static bool IsChord(string token) => ChordToken().IsMatch(token);

    /// <summary>A line made of chords only (bars and repeats allowed), with at least one chord.</summary>
    public static bool IsChordLine(string line)
    {
        var tokens = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        return tokens.Any(IsChord) && tokens.All(t => IsChord(t) || Filler().IsMatch(t));
    }

    /// <summary>The pasted sheet as lines, chord lines moved <paramref name="shift"/> half steps.</summary>
    public static IReadOnlyList<SheetLine> Lines(string? sheet, int shift) =>
        Clean(sheet).Split('\n').Select(l =>
            l.Trim().Length == 0 ? new SheetLine(SheetLineKind.Blank, "")
            : SectionLine().IsMatch(l) ? new SheetLine(SheetLineKind.Section, l.Trim().Trim('[', ']'))
            : IsChordLine(l) ? new SheetLine(SheetLineKind.Chords, TransposeLine(l, shift))
            : new SheetLine(SheetLineKind.Text, l.TrimEnd())).ToList();

    /// <summary>Every chord on a chord line moved. Each chord starts in the same column as before (over the same word);
    /// only when the one before it grew too long is it pushed right, one space after it.</summary>
    public static string TransposeLine(string line, int shift)
    {
        if (shift % 12 == 0) return line.TrimEnd();
        var sb = new StringBuilder();
        var i = 0;
        while (i < line.Length)
        {
            if (line[i] == ' ') { i++; continue; }
            var end = i;
            while (end < line.Length && line[end] != ' ') end++;
            var token = line[i..end];
            var column = sb.Length == 0 ? i : Math.Max(i, sb.Length + 1);
            sb.Append(' ', column - sb.Length).Append(IsChord(token) ? TransposeChord(token, shift) : token);
            i = end;
        }
        return sb.ToString();
    }

    /// <summary>"F#m7/A" two half steps down -> "Em7/G".</summary>
    public static string TransposeChord(string chord, int shift)
    {
        var slash = chord.IndexOf('/');
        return slash < 0 ? Chords.Transpose(chord, shift)
            : Chords.Transpose(chord[..slash], shift) + "/" + Chords.Transpose(chord[(slash + 1)..], shift);
    }

    /// <summary>The chords on the sheet, each once, in the order they come.</summary>
    public static IReadOnlyList<string> ChordsIn(string? sheet) =>
        Clean(sheet).Split('\n').Where(IsChordLine)
            .SelectMany(l => l.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Where(IsChord))
            .Distinct().ToList();

    /// <summary>The move that makes the sheet's chords the song's grips (a sheet in E and a song with capo 2 in D grips
    /// -> -2). On a tie the move the capo explains wins (a sheet in the original key). 0 when the song has no chords yet.</summary>
    public static int BestShift(string? sheet, IEnumerable<string> songChords, int capo = 0)
    {
        var mine = songChords.Select(Root).Where(r => r is not null).ToHashSet();
        var theirs = ChordsIn(sheet).Select(c => c.Split('/')[0]).ToList();
        if (mine.Count == 0 || theirs.Count == 0) return 0;
        return Enumerable.Range(-6, 12)
            .Select(s => (Shift: s, Hits: theirs.Count(c => mine.Contains(Root(Chords.Transpose(c, s))))))
            .OrderByDescending(x => x.Hits).ThenByDescending(x => x.Shift == ToRange(-capo)).ThenBy(x => Math.Abs(x.Shift)).ThenBy(x => x.Shift)
            .First().Shift;
    }

    /// <summary>-6..5, the shortest way round.</summary>
    public static int ToRange(int semitones) { var s = ((semitones % 12) + 12) % 12; return s > 5 ? s - 12 : s; }

    /// <summary>The capo that matches a move: a sheet moved -2 is played with capo 2 to sound as written.</summary>
    public static int CapoFor(int shift) => ((-shift) % 12 + 12) % 12;

    // Root + minor/major only, so "F#m7" on the sheet meets "Em" in the song after the move.
    private static string? Root(string chord) =>
        Chords.Split(chord) is { } s && Chords.RootIndex(chord) is { } i
            ? Chords.Transpose("C", i) + (Chords.IsMinor(chord) ? "m" : "")
            : null;

    private static string Clean(string? sheet) => (sheet ?? "").Replace("\r\n", "\n").Replace('\t', ' ').Replace(' ', ' ');
}

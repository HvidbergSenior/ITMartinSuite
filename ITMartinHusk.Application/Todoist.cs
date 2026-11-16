using System.Globalization;
using System.Text;
using ITMartinHusk.Domain;

namespace ITMartinHusk.Application;

/// <summary>
/// "Hent fra Todoist" (user 2026-11-10, after Jeppe in Jacob's FB thread: Todoist "just raised the price a lot ... my systems are
/// integrated with it"): a project exported from Todoist as CSV (Project -> ... -> Export as template -> CSV) becomes balls.
/// Columns are found by name (TYPE, CONTENT, DESCRIPTION, PRIORITY, INDENT, DATE); TYPE task / section / note (lowercase);
/// INDENT 2+ = a sub-task; PRIORITY 1 = highest. A section name becomes the area, else the project's file name.
/// The first sub-task becomes the next step; a Todoist date Husk cannot read is kept in the note. Up to 300 tasks (Todoist's own limit).
/// </summary>
public static class TodoistImport
{
    public const int MaxTasks = 300;

    public sealed record Parsed(List<BallInput> Balls, int Skipped, string? Error);

    public static Parsed Parse(string? csv, string? fileName, DateTime now)
    {
        var rows = Rows(csv ?? "");
        if (rows.Count < 2) return new([], 0, "Filen er tom. Eksportér projektet fra Todoist som CSV og prøv igen.");
        var head = rows[0].Select(h => h.Trim().TrimStart('﻿').ToUpperInvariant()).ToList();
        int Col(string name) => head.IndexOf(name);
        int type = Col("TYPE"), content = Col("CONTENT"), desc = Col("DESCRIPTION"), prio = Col("PRIORITY"), indent = Col("INDENT"), date = Col("DATE");
        if (type < 0 || content < 0) return new([], 0, "Det ligner ikke en fil fra Todoist (der mangler TYPE og CONTENT). Brug Eksportér som skabelon → CSV.");
        string At(List<string> r, int i) => i >= 0 && i < r.Count ? r[i].Trim() : "";

        var project = Path.GetFileNameWithoutExtension(fileName ?? "").Trim();
        var area = project.Length > 0 ? project : "Todoist";
        var balls = new List<(BallInput Ball, List<string> Subs, StringBuilder Note)>();
        var skipped = 0;
        foreach (var r in rows.Skip(1))
        {
            var kind = At(r, type).ToLowerInvariant();
            var text = WithoutLabels(At(r, content), out var labels);
            if (kind == "section") { if (text.Length > 0) area = text; continue; }
            if (kind == "note")
            {
                if (balls.Count > 0 && text.Length > 0) Append(balls[^1].Note, text);
                continue;
            }
            if (kind != "task" || text.Length == 0) continue;
            if (int.TryParse(At(r, indent), out var level) && level >= 2)
            {
                if (balls.Count > 0) balls[^1].Subs.Add(text);
                continue;
            }
            if (balls.Count >= MaxTasks) { skipped++; continue; }
            var note = new StringBuilder();
            Append(note, At(r, desc));
            if (At(r, prio) == "1") Append(note, "Todoist: prioritet 1 (haster mest).");
            if (labels.Count > 0) Append(note, "Etiketter: " + string.Join(", ", labels));
            var rawDate = At(r, date);
            var deadline = ReadDate(rawDate, now);
            if (deadline is null && rawDate.Length > 0) Append(note, "Todoist-dato: " + rawDate);
            balls.Add((new BallInput(null, text, area, "i gang", Deadline: deadline), [], note));
        }
        var result = balls.Select(b =>
        {
            var note = b.Note;
            if (b.Subs.Count > 1) Append(note, "Delopgaver: " + string.Join("; ", b.Subs.Skip(1)));
            return b.Ball with { NextStep = b.Subs.FirstOrDefault() ?? "", Note = note.ToString() };
        }).ToList();
        return new(result, skipped, result.Count == 0 ? "Der var ingen opgaver i filen." : null);
    }

    private static void Append(StringBuilder sb, string s)
    {
        if (s.Length == 0) return;
        if (sb.Length > 0) sb.Append(' ');
        sb.Append(s);
    }

    /// <summary>"Ring til tandlægen @telefon @haster" -> "Ring til tandlægen" + [telefon, haster].</summary>
    private static string WithoutLabels(string s, out List<string> labels)
    {
        labels = [];
        var words = new List<string>();
        foreach (var w in s.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            if (w.Length > 1 && w[0] == '@') labels.Add(w[1..]); else words.Add(w);
        return string.Join(' ', words);
    }

    private static readonly string[] Formats = ["yyyy-MM-dd", "yyyy-MM-dd HH:mm", "yyyy-MM-ddTHH:mm:ss", "d/M/yyyy", "d-M-yyyy", "d.M.yyyy",
        "d MMM yyyy", "MMM d yyyy", "d MMM", "MMM d", "d MMMM yyyy", "MMMM d yyyy"];

    /// <summary>A plain date (2026-11-20, 20/11/2026, 20 Nov 2026, "today", "tomorrow", "i dag", "i morgen"); a repeating or
    /// other natural-language date ("every monday") is not guessed - it goes in the note.</summary>
    public static DateTime? ReadDate(string s, DateTime now)
    {
        var t = s.Trim().TrimEnd('.').Replace(",", "");
        if (t.Length == 0) return null;
        switch (t.ToLowerInvariant())
        {
            case "today" or "i dag": return now.Date;
            case "tomorrow" or "i morgen": return now.Date.AddDays(1);
        }
        foreach (var culture in new[] { CultureInfo.InvariantCulture, new CultureInfo("da-DK") })
            if (DateTime.TryParseExact(t, Formats, culture, DateTimeStyles.AllowWhiteSpaces, out var d))
            {
                // "20 Nov" without a year = the next 20 Nov.
                if (!t.Any(char.IsDigit) || t.Count(char.IsDigit) <= 2) { if (d.Date < now.Date) d = d.AddYears(1); }
                return d.Date;
            }
        return null;
    }

    /// <summary>A small RFC 4180 reader: commas, quotes, "" inside quotes, line breaks inside quotes.</summary>
    private static List<List<string>> Rows(string csv)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var cell = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < csv.Length; i++)
        {
            var c = csv[i];
            if (quoted)
            {
                if (c == '"' && i + 1 < csv.Length && csv[i + 1] == '"') { cell.Append('"'); i++; }
                else if (c == '"') quoted = false;
                else cell.Append(c);
                continue;
            }
            switch (c)
            {
                case '"': quoted = true; break;
                case ',': row.Add(cell.ToString()); cell.Clear(); break;
                case '\r': break;
                case '\n': row.Add(cell.ToString()); cell.Clear(); rows.Add(row); row = []; break;
                default: cell.Append(c); break;
            }
        }
        if (cell.Length > 0 || row.Count > 0) { row.Add(cell.ToString()); rows.Add(row); }
        return rows.Where(r => r.Any(x => x.Trim().Length > 0)).ToList();
    }
}

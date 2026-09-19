using ITMartinPolstrer.Core.Data.Entities;

namespace ITMartinPolstrer.Core.Services;

// "Have I done this before?" - the search behind the Erfaring page. Pure
// in-memory: a single upholsterer's whole career is a few hundred pieces,
// so a full scan per keystroke is fine and far simpler than SQLite FTS.
public static class ExperienceSearch
{
    public sealed record Hit(Piece Piece, bool InstructionsHit, List<Step> Steps);

    public static List<Hit> Find(IEnumerable<Piece> pieces, string query)
    {
        var q = query.Trim();
        var hits = new List<Hit>();
        if (q.Length == 0) return hits;

        var cmp = StringComparison.OrdinalIgnoreCase;
        foreach (var p in pieces)
        {
            var instr = p.Instructions.Contains(q, cmp);
            var steps = p.Steps.Where(s => s.Note.Contains(q, cmp)).OrderBy(s => s.At).ToList();
            var meta = p.Title.Contains(q, cmp) || p.Customer.Contains(q, cmp)
                    || p.TechniqueList.Any(t => t.Contains(q, cmp));
            if (instr || steps.Count > 0 || meta) hits.Add(new Hit(p, instr, steps));
        }
        return hits;
    }

    // Technique tags across all pieces, most used first, case-insensitive.
    public static List<string> Tags(IEnumerable<Piece> pieces) =>
        pieces.SelectMany(p => p.TechniqueList)
            .GroupBy(t => t.ToLowerInvariant())
            .OrderByDescending(g => g.Count()).ThenBy(g => g.Key)
            .Select(g => g.First()).ToList();

    // A short window of text around the first match, so a long note still
    // shows *why* it matched.
    public static string Snippet(string text, string query)
    {
        var i = text.IndexOf(query, StringComparison.OrdinalIgnoreCase);
        if (i < 0 || text.Length <= 140) return text;
        var start = Math.Max(0, i - 50);
        var len = Math.Min(140, text.Length - start);
        return (start > 0 ? "…" : "") + text.Substring(start, len) + (start + len < text.Length ? "…" : "");
    }
}

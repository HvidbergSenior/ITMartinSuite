namespace ITMartinSkrivSange.Domain;

/// <param name="ProgressionId">The library progression (or own "own-..." one), or null when the chords were made for this part.</param>
/// <param name="Why">One line telling why it fits, shown with the suggestion.</param>
public sealed record Suggestion(IReadOnlyList<string> Chords, string? ProgressionId, string Why);

// "Foreslå" on a part: chords that fit the rest of the song.
// The part looks at what comes before it (user 2026-10-14: "my intro is Am G Am G E ... verse should be looking at that"):
//  - later verses repeat the first verse with chords,
//  - a verse with no verse before it starts from the part before it: the same chords, then progressions in the same key
//    sharing the most chords with it,
//  - the bridge is the contrast (same key, starts elsewhere, other mode if it can),
//  - the outro is the verse (or the part before) ending on the home chord,
//  - with nothing before it at all, the song's mood decides.
// Pressing again gives the next candidate, round and round.
public static class ProgressionAdvisor
{
    public static Suggestion Suggest(Song song, PartKind kind)
    {
        var current = song.Part(kind).Chords;
        var lead = song.LeadVerse(except: kind);
        var before = song.PartBefore(kind);
        var candidates = kind switch
        {
            _ when Parts.IsVerse(kind) && lead is not null && lead.Kind < kind => Repeat(lead),
            PartKind.Bridge when (lead ?? before) is { } anchor => Bridge(anchor),
            PartKind.Outro when (lead ?? before) is { } anchor => [Outro(anchor)],
            PartKind.Intro when lead is not null => [new(lead.Chords, lead.ProgressionId, "Versets akkorder én gang, blødt - så kender lytteren dem, når du begynder at synge.")],
            _ when before is not null => FollowOn(before),
            _ when lead is not null => Repeat(lead),   // Vers 1 when only Vers 2/3 have chords
            _ => FromMood(song),
        };
        return Next(candidates, current);
    }

    private static List<Suggestion> Repeat(SongPart lead) =>
        [new(lead.Chords, lead.ProgressionId, $"Samme akkorder som {lead.Label} - versene lyder bedst ens, det er teksten der skifter.")];

    // The same chords as the part before, then library progressions in its key that share the most chords with it.
    private static List<Suggestion> FollowOn(SongPart before)
    {
        var home = Home(before);
        var family = Chords.MajorFamily(home);
        var own = before.Chords.Distinct().ToHashSet();
        var list = new List<Suggestion>
        {
            new(before.Chords, before.ProgressionId, $"Samme akkorder som {before.Label} - så hænger delene sammen."),
        };
        list.AddRange(ProgressionLibrary.All
            .Where(p => p.Family == family && !p.Chords.SequenceEqual(before.Chords))
            .Select(p => (p, Shared: p.Chords.Distinct().Count(own.Contains)))
            .OrderByDescending(x => x.Shared)
            .ThenBy(x => x.p.Minor == Chords.IsMinor(home) ? 0 : 1)
            .Select(x => new Suggestion(x.p.Chords, x.p.Id, x.Shared > 0
                ? $"{x.p.Name}: samme toneart som {before.Label} ({Chords.KeyName(home)}) og bruger {string.Join(", ", x.p.Chords.Distinct().Where(own.Contains))} ligesom den - fortsætter hvor {before.Label.ToLowerInvariant()} slap."
                : $"{x.p.Name}: samme toneart som {before.Label} ({Chords.KeyName(home)}), men nye akkorder - {x.p.Feel.ToLowerInvariant()}.")));
        return list;
    }

    // Same key (so it belongs to the song) but another way in: starts on another chord, and preferably the other mode (mol/dur).
    private static List<Suggestion> Bridge(SongPart anchor)
    {
        var home = Home(anchor);
        var family = Chords.MajorFamily(home);
        var minor = Chords.IsMinor(home);
        var list = ProgressionLibrary.All
            .Where(p => p.Family == family && p.Id != anchor.ProgressionId && p.Chords[0] != anchor.Chords[0])
            .OrderBy(p => p.Minor == minor ? 1 : 0)
            .Select(p =>
            {
                var mode = p.Minor == minor ? "" : p.Minor ? " og går i mol" : " og går i dur";
                return new Suggestion(p.Chords, p.Id, $"{p.Name}: samme toneart som {anchor.Label.ToLowerInvariant()}, men starter på {p.Chords[0]}{mode} - broen skal lyde anderledes.");
            })
            .ToList();
        // No library progression in this key: turn the part around, so it starts somewhere new.
        if (list.Count == 0)
            list.Add(new(anchor.Chords.Reverse().ToList(), null, $"{anchor.Label}s akkorder baglæns - samme toneart, men det lyder som et nyt sted."));
        return list;
    }

    private static Suggestion Outro(SongPart anchor)
    {
        var home = Home(anchor);
        var chords = anchor.Chords[^1] == home ? anchor.Chords : [.. anchor.Chords, home];
        return new(chords, null, $"{anchor.Label}s akkorder, og slut på {Chords.KeyName(home)} - så lyder sangen færdig.");
    }

    private static List<Suggestion> FromMood(Song song) =>
        ProgressionLibrary.ForMood(song.Mood)
            .Select(p => new Suggestion(p.Chords, p.Id, $"{p.Name} - {p.Feel.ToLowerInvariant()}. Passer til stemningen {song.Mood.ToLowerInvariant()}."))
            .ToList();

    // The key a part is in: its library progression's home, else its first chord.
    private static string Home(SongPart part) => ProgressionLibrary.Find(part.ProgressionId)?.Home ?? part.Chords[0];

    // The candidate after the one the part has now, round and round.
    private static Suggestion Next(IReadOnlyList<Suggestion> list, IReadOnlyList<string> current)
    {
        var i = list.ToList().FindIndex(s => s.Chords.SequenceEqual(current));
        return list[(i + 1) % list.Count];
    }
}

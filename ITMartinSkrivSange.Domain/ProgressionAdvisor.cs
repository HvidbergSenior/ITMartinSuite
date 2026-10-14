namespace ITMartinSkrivSange.Domain;

/// <param name="ProgressionId">The library progression, or null when the chords were made for this part (the outro).</param>
/// <param name="Why">One line telling why it fits, shown with the suggestion.</param>
public sealed record Suggestion(IReadOnlyList<string> Chords, string? ProgressionId, string Why);

// "Foreslå" on a part (plan 2026-10-14): chords that fit the rest of the song.
// Verses are alike, the bridge is the contrast, intro and outro grow out of the verse.
// Pressing again on a verse without a lead verse, or on the bridge, gives the next candidate.
public static class ProgressionAdvisor
{
    public static Suggestion Suggest(Song song, PartKind kind)
    {
        var lead = song.LeadVerse(except: kind);
        var current = song.Part(kind).ProgressionId;

        if (lead is null)
        {
            // Nothing to follow yet: start from the song's mood.
            var p = Next(ProgressionLibrary.ForMood(song.Mood), current);
            return new(p.Chords, p.Id, $"{p.Name} - {p.Feel.ToLowerInvariant()}. Passer til stemningen {song.Mood.ToLowerInvariant()}.");
        }

        var leadProg = ProgressionLibrary.Find(lead.ProgressionId);
        var home = leadProg?.Home ?? lead.Chords[0];

        switch (kind)
        {
            case PartKind.Bridge:
                return Bridge(lead, leadProg, home, current);

            case PartKind.Intro:
                return new(lead.Chords, lead.ProgressionId, $"Versets akkorder én gang, blødt - så kender lytteren dem, når du begynder at synge.");

            case PartKind.Outro:
                var chords = lead.Chords[^1] == home ? lead.Chords : [.. lead.Chords, home];
                return new(chords, null, $"Versets akkorder, og slut på {Chords.KeyName(home)} - så lyder sangen færdig.");

            default:
                return new(lead.Chords, lead.ProgressionId, $"Samme akkorder som {lead.Label} - versene lyder bedst ens, det er teksten der skifter.");
        }
    }

    // Same key (so it belongs to the song) but another way in: starts on another chord, and preferably the other mode (mol/dur).
    private static Suggestion Bridge(SongPart lead, Progression? leadProg, string home, string? current)
    {
        var family = leadProg?.Family ?? Chords.MajorFamily(home);
        var minor = Chords.IsMinor(home);
        var candidates = ProgressionLibrary.All
            .Where(p => p.Family == family && p.Id != lead.ProgressionId && p.Chords[0] != lead.Chords[0])
            .OrderBy(p => p.Minor == minor ? 1 : 0)
            .ToList();

        if (candidates.Count == 0)
        {
            // No library progression in this key: turn the verse around, so it starts somewhere new.
            var turned = lead.Chords.Reverse().ToList();
            return new(turned, null, "Versets akkorder baglæns - samme toneart, men det lyder som et nyt sted.");
        }

        var p = Next(candidates, current);
        var mode = p.Minor == minor ? "" : p.Minor ? " og går i mol" : " og går i dur";
        return new(p.Chords, p.Id, $"{p.Name}: samme toneart som versene, men starter på {p.Chords[0]}{mode} - broen skal lyde anderledes.");
    }

    // The one after the current, round and round.
    private static Progression Next(IReadOnlyList<Progression> list, string? currentId)
    {
        var i = list.ToList().FindIndex(p => p.Id == currentId);
        return list[(i + 1) % list.Count];
    }
}

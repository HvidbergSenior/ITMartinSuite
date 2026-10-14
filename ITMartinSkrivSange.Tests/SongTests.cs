using FluentAssertions;
using ITMartinSkrivSange.Application;
using ITMartinSkrivSange.Domain;

namespace ITMartinSkrivSange.Tests;

public class ChordTests
{
    [TestCase("G", 2, "A")]
    [TestCase("Em", 2, "F#m")]
    [TestCase("C", 3, "Eb")]
    [TestCase("Fmaj7", 1, "F#maj7")]
    [TestCase("B7", 1, "C7")]
    public void Capo_raises_the_grip(string grip, int capo, string sounding) => Capo.Sounding(grip, capo).Should().Be(sounding);

    [TestCase("Em", "G")]
    [TestCase("Am", "C")]
    [TestCase("Dm", "F")]
    [TestCase("G", "G")]
    [TestCase("A7", "A")]
    public void Minor_keys_belong_to_the_major_key_three_frets_up(string home, string family) => Chords.MajorFamily(home).Should().Be(family);

    [Test]
    public void Maj7_is_not_minor() => Chords.IsMinor("Fmaj7").Should().BeFalse();

    [Test]
    public void Every_library_progression_is_open_grips_only() =>
        ProgressionLibrary.All.SelectMany(p => p.Chords).Should().OnlyContain(c => Chords.IsOpenGrip(c));

    [Test]
    public void Every_mood_has_progressions() =>
        ProgressionLibrary.Moods.Should().OnlyContain(m => ProgressionLibrary.ForMood(m).Count >= 3);
}

public class SongRuleTests
{
    private static readonly DateTime Now = new(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc);

    [Test]
    public void A_new_song_has_the_six_parts_in_order()
    {
        var song = Song.Create("Min sang", Now);
        song.Parts.Select(p => p.Label).Should().Equal("Intro", "Vers 1", "Vers 2", "Bridge", "Vers 3", "Outro");
    }

    [Test]
    public void A_title_is_required() => FluentActions.Invoking(() => Song.Create("  ", Now)).Should().Throw<SongException>();

    [Test]
    public void Barre_chords_are_refused()
    {
        var song = Song.Create("x", Now);
        FluentActions.Invoking(() => song.UseChords(PartKind.Vers1, ["F", "Bm"], null, Now))
            .Should().Throw<SongException>().WithMessage("*capo*");
    }

    [Test]
    public void Capo_outside_0_to_7_is_refused()
    {
        var song = Song.Create("x", Now);
        FluentActions.Invoking(() => song.SetCapo(8, Now)).Should().Throw<SongException>();
    }

    [Test]
    public void Notes_keep_the_trailing_space_while_typing()
    {
        var song = Song.Create("x", Now);
        song.WriteNotes(PartKind.Vers1, "Jeg kigger ", Now);
        song.Part(PartKind.Vers1).Notes.Should().Be("Jeg kigger ");
    }

    [Test]
    public void Restore_puts_the_parts_back_in_order_and_fills_the_missing()
    {
        var song = Song.Restore(Guid.NewGuid(), "x", "Glad", 99, 5, "nope",
            [new SongPart(PartKind.Outro, ["G"], null, "slut"), new SongPart(PartKind.Intro, ["C"], null, "")], Now, Now);
        song.Parts.Should().HaveCount(6);
        song.Part(PartKind.Outro).Notes.Should().Be("slut");
        song.Capo.Should().Be(Capo.Max);
        song.Bpm.Should().Be(Song.MinBpm);
        song.StrumId.Should().Be(Strums.Default.Id);
    }

    [Test]
    public void Summary_tells_the_sounding_key_with_capo()
    {
        var song = Song.Create("x", Now);
        song.UseProgression(PartKind.Vers1, ProgressionLibrary.Find("glad-pop")!, Now);
        song.SetCapo(2, Now);
        song.Summary().Should().StartWith("Klinger i A (capo 2, greb i G)").And.Contain("1 af 6 dele har akkorder");
    }
}

public class AdvisorTests
{
    private static readonly DateTime Now = new(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc);

    private static Song WithVerse(string progressionId)
    {
        var song = Song.Create("x", Now);
        song.UseProgression(PartKind.Vers1, ProgressionLibrary.Find(progressionId)!, Now);
        return song;
    }

    [Test]
    public void Without_a_verse_it_starts_from_the_mood()
    {
        var song = Song.Create("x", Now);
        song.SetMood("Trist", Now);
        var s = ProgressionAdvisor.Suggest(song, PartKind.Vers1);
        ProgressionLibrary.Find(s.ProgressionId)!.Mood.Should().Be("Trist");
    }

    [Test]
    public void Pressing_again_gives_the_next_one()
    {
        var song = Song.Create("x", Now);
        var first = ProgressionAdvisor.Suggest(song, PartKind.Vers1);
        song.UseChords(PartKind.Vers1, first.Chords, first.ProgressionId, Now);
        ProgressionAdvisor.Suggest(song, PartKind.Vers1).ProgressionId.Should().NotBe(first.ProgressionId);
    }

    [Test]
    public void Other_verses_follow_the_lead_verse()
    {
        var s = ProgressionAdvisor.Suggest(WithVerse("glad-pop"), PartKind.Vers3);
        s.Chords.Should().Equal("G", "D", "Em", "C");
    }

    [Test]
    public void The_bridge_stays_in_the_key_but_starts_elsewhere_and_prefers_the_other_mode()
    {
        var s = ProgressionAdvisor.Suggest(WithVerse("glad-pop"), PartKind.Bridge);
        var p = ProgressionLibrary.Find(s.ProgressionId)!;
        p.Family.Should().Be("G");
        p.Chords[0].Should().NotBe("G");
        p.Minor.Should().BeTrue();
    }

    [Test]
    public void The_outro_ends_on_the_home_chord()
    {
        var s = ProgressionAdvisor.Suggest(WithVerse("glad-pop"), PartKind.Outro);
        s.Chords.Should().Equal("G", "D", "Em", "C", "G");
    }

    [Test]
    public void Every_suggestion_can_be_used()
    {
        foreach (var p in ProgressionLibrary.All)
        {
            var song = WithVerse(p.Id);
            foreach (var kind in Parts.Order)
            {
                var s = ProgressionAdvisor.Suggest(song, kind);
                song.UseChords(kind, s.Chords, s.ProgressionId, Now);
            }
        }
    }
}

public class SongWritingTests
{
    private sealed class MemoryStore : ISongStore
    {
        public readonly Dictionary<Guid, Song> Songs = [];
        public int Saves;
        public Task<IReadOnlyList<Song>> ListAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Song>>(Songs.Values.ToList());
        public Task<Song?> GetAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Songs.GetValueOrDefault(id));
        public Task SaveAsync(Song song, CancellationToken ct = default) { Songs[song.Id] = song; Saves++; return Task.CompletedTask; }
        public Task DeleteAsync(Guid id, CancellationToken ct = default) { Songs.Remove(id); return Task.CompletedTask; }
    }

    private sealed class MemoryOwn : IOwnProgressionStore
    {
        public readonly List<OwnProgression> All = [];
        public Task<IReadOnlyList<OwnProgression>> ListAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<OwnProgression>>(All.ToList());
        public Task AddAsync(OwnProgression p, CancellationToken ct = default) { All.Add(p); return Task.CompletedTask; }
        public Task DeleteAsync(Guid id, CancellationToken ct = default) { All.RemoveAll(p => p.Id == id); return Task.CompletedTask; }
    }

    [Test]
    public async Task Typed_chords_are_used_and_saved_as_MartinChord_once()
    {
        var own = new MemoryOwn();
        var writing = new SongWriting(new MemoryStore(), own, TimeProvider.System);
        var song = await writing.CreateAsync("x");

        var first = await writing.UseOwnChordsAsync(song, PartKind.Vers1, "am, G, Am - g", null);
        var again = await writing.UseOwnChordsAsync(song, PartKind.Vers2, "Am G Am G", null);
        var second = await writing.UseOwnChordsAsync(song, PartKind.Bridge, "C D Em", null);

        first.Name.Should().Be("MartinChord 1");
        again.Id.Should().Be(first.Id);
        second.Name.Should().Be("MartinChord 2");
        own.All.Should().HaveCount(2);
        song.Part(PartKind.Vers1).Chords.Should().Equal("Am", "G", "Am", "G");

        // Picked again in another part from the list.
        await writing.UseProgressionAsync(song, PartKind.Outro, first.ProgressionId);
        song.Part(PartKind.Outro).Chords.Should().Equal("Am", "G", "Am", "G");
    }

    [Test]
    public async Task Every_change_is_saved()
    {
        var store = new MemoryStore();
        var writing = new SongWriting(store, new MemoryOwn(), TimeProvider.System);
        var song = await writing.CreateAsync("Stjerner");
        var why = await writing.SuggestAndUseAsync(song, PartKind.Vers1);
        await writing.WriteNotesAsync(song, PartKind.Vers1, "Jeg kigger på stjerner");

        store.Saves.Should().Be(3);
        why.Why.Should().NotBeNullOrWhiteSpace();
        store.Songs[song.Id].Part(PartKind.Vers1).HasChords.Should().BeTrue();
    }
}

public class OwnChordTests
{
    [Test]
    public void Unknown_or_barre_chords_are_refused_with_a_hint() =>
        FluentActions.Invoking(() => ChordInput.Parse("Am F#m")).Should().Throw<SongException>().WithMessage("F#m*capo*");

    [Test]
    public void A_given_name_is_kept_and_numbering_continues_after_the_highest()
    {
        var now = DateTime.UtcNow;
        var list = new List<OwnProgression> { new(Guid.NewGuid(), "MartinChord 3", ["C"], now), new(Guid.NewGuid(), "Min regnsang", ["G"], now) };
        OwnProgression.NextName(list).Should().Be("MartinChord 4");
        OwnProgression.FindOrCreate(list, ["Am", "G"], "  Aftensang ", now).Progression.Name.Should().Be("Aftensang");
    }
}

public class ChordIdeaTests
{
    [Test]
    public void Em_in_G_can_be_swapped_for_Em7_or_a_chord_sharing_two_notes()
    {
        var alts = ChordAdvisor.Alternatives("Em", "G").Select(i => i.Chord).ToList();
        alts.Should().Contain("Em7").And.Contain("G").And.Contain("C");
        alts.Should().NotContain("Em");
    }

    [Test]
    public void After_D_in_G_it_goes_home_to_G()
    {
        var next = ChordAdvisor.Next("D", "G");
        next[0].Chord.Should().Be("G");
    }

    [Test]
    public void In_C_the_IV_chord_is_Fmaj7_because_F_is_a_barre()
    {
        ChordAdvisor.Next("C", "C").Select(i => i.Chord).Should().Contain("Fmaj7").And.Contain("G").And.Contain("Am");
    }

    [Test]
    public void Every_idea_is_an_open_grip()
    {
        foreach (var p in ProgressionLibrary.All)
        foreach (var c in p.Chords)
            ChordAdvisor.Alternatives(c, p.Home).Concat(ChordAdvisor.Next(c, p.Home))
                .Should().OnlyContain(i => Chords.IsOpenGrip(i.Chord));
    }

    [Test]
    public void Swapping_a_chord_makes_the_part_your_own()
    {
        var now = DateTime.UtcNow;
        var song = Song.Create("x", now);
        song.UseProgression(PartKind.Vers1, ProgressionLibrary.Find("glad-pop")!, now);
        song.ReplaceChord(PartKind.Vers1, 2, "Em7", now);
        song.InsertChordAfter(PartKind.Vers1, 3, "D", now);
        song.Part(PartKind.Vers1).Chords.Should().Equal("G", "D", "Em7", "C", "D");
        song.Part(PartKind.Vers1).ProgressionId.Should().BeNull();
    }
}

public class GripTests
{
    [Test]
    public void Every_open_grip_has_a_chord_box() => Chords.OpenGrips.Should().OnlyContain(c => Grips.For(c) != null);

    [Test]
    public void Fingers_only_where_a_fret_is_pressed() =>
        Chords.OpenGrips.Select(c => Grips.For(c)!).Should().OnlyContain(g =>
            g.Frets.Count == 6 && g.Frets.Zip(g.Fingers).All(x => (x.First > 0) == (x.Second > 0)));

    [Test]
    public void The_grip_plays_the_notes_of_the_chord()
    {
        int[] open = [4, 9, 2, 7, 11, 4];   // E A D G B E
        foreach (var c in Chords.OpenGrips)
        {
            var g = Grips.For(c)!;
            var played = g.Frets.Select((f, s) => f < 0 ? -1 : (open[s] + f) % 12).Where(n => n >= 0).ToHashSet();
            played.Should().BeSubsetOf(ChordAdvisor.Notes(c), c);
        }
    }
}

public class StrumTests
{
    [Test]
    public void Every_pattern_is_one_bar_of_known_steps() =>
        Strums.All.Should().OnlyContain(s => s.Pattern.Length == 8 && s.Pattern.All(c => StrumPattern.Steps.Contains(c)) && Strums.Kinds.Contains(s.Kind));

    [Test]
    public void There_is_strumming_picking_and_both() =>
        Strums.Kinds.Should().OnlyContain(k => Strums.All.Any(s => s.Kind == k));
}

public class ChordsInNotesTests
{
    [Test]
    public void Notes_holding_only_chords_are_recognised() =>
        ChordInput.TryParse("Am, G, Am, G, E").Should().Equal("Am", "G", "Am", "G", "E");

    [Test]
    public void Real_lyrics_are_not_taken_for_chords() =>
        ChordInput.TryParse("Jeg kigger på stjerner").Should().BeNull();
}

public class OnTheFlyTests
{
    [Test]
    public void A_part_can_be_built_chord_by_chord_and_emptied_again()
    {
        var now = DateTime.UtcNow;
        var song = Song.Create("x", now);
        song.InsertChordAfter(PartKind.Intro, -1, "Am", now);
        song.InsertChordAfter(PartKind.Intro, 0, "G", now);
        song.Part(PartKind.Intro).Chords.Should().Equal("Am", "G");
        song.RemoveChord(PartKind.Intro, 0, now);
        song.RemoveChord(PartKind.Intro, 0, now);
        song.Part(PartKind.Intro).HasChords.Should().BeFalse();
    }

    [Test]
    public void No_more_than_eight_chords()
    {
        var now = DateTime.UtcNow;
        var song = Song.Create("x", now);
        for (var i = 0; i < Song.MaxChords; i++) song.InsertChordAfter(PartKind.Intro, i - 1, "G", now);
        FluentActions.Invoking(() => song.InsertChordAfter(PartKind.Intro, 7, "C", now)).Should().Throw<SongException>();
    }
}

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

    [Test]
    public async Task Every_change_is_saved()
    {
        var store = new MemoryStore();
        var writing = new SongWriting(store, TimeProvider.System);
        var song = await writing.CreateAsync("Stjerner");
        var why = await writing.SuggestAndUseAsync(song, PartKind.Vers1);
        await writing.WriteNotesAsync(song, PartKind.Vers1, "Jeg kigger på stjerner");

        store.Saves.Should().Be(3);
        why.Why.Should().NotBeNullOrWhiteSpace();
        store.Songs[song.Id].Part(PartKind.Vers1).HasChords.Should().BeTrue();
    }
}

using FluentAssertions;
using ITMartinSkrivSange.Domain;

namespace ITMartinSkrivSange.Tests;

// Tekst med akkorder (user 2026-10-29). The sheet text below is made up - no real lyrics in the tests.
public class ChordSheetTests
{
    private const string Sheet =
        "[Verse]\n" +
        "E        F#m7/A   B7\n" +
        "Here is a made up line\n" +
        "\n" +
        "[Chorus]\n" +
        "C#m      A   Am    E  (x2)\n" +
        "Another line I wrote myself\n";

    [TestCase("E", true)]
    [TestCase("F#m7/A", true)]
    [TestCase("B7b9", true)]
    [TestCase("A+", true)]
    [TestCase("Csus4", true)]
    [TestCase("Ebmaj7", true)]
    [TestCase("Am", true)]
    [TestCase("And", false)]
    [TestCase("Every", false)]
    [TestCase("Bad", false)]
    public void A_chord_token_is_told_from_a_word(string token, bool chord) => ChordSheet.IsChord(token).Should().Be(chord);

    [TestCase("E        F#m7/A   B7", true)]
    [TestCase("C#m  A  |  Am  E  (x2)", true)]
    [TestCase("A day in E", false)]
    [TestCase("Bad Day", false)]
    public void A_chord_line_holds_only_chords(string line, bool chords) => ChordSheet.IsChordLine(line).Should().Be(chords);

    [Test]
    public void The_lines_are_sorted_and_only_chord_lines_move()
    {
        var lines = ChordSheet.Lines(Sheet, -2);
        lines.Select(l => l.Kind).Should().StartWith([SheetLineKind.Section, SheetLineKind.Chords, SheetLineKind.Text, SheetLineKind.Blank, SheetLineKind.Section]);
        lines[0].Text.Should().Be("Verse");
        lines[1].Text.Should().Be("D        Em7/G    A7");
        lines[2].Text.Should().Be("Here is a made up line");
        lines[5].Text.Should().Be("Bm       G   Gm    D  (x2)");
    }

    [Test]
    public void A_chord_that_grows_keeps_the_next_one_over_its_word()
    {
        ChordSheet.TransposeLine("E    A", 1).Should().Be("F    Bb");
        ChordSheet.TransposeLine("E A", -1).Should().Be("Eb Ab");      // pushed one space right, never glued together
        ChordSheet.TransposeLine("  G   C", 2).Should().Be("  A   D");
    }

    [Test]
    public void The_move_meets_the_song_grips()
    {
        // A sheet in E, the song in D grips with capo 2.
        ChordSheet.BestShift(Sheet, ["D", "Em7", "A7", "Bm", "G", "Gm"]).Should().Be(-2);
        ChordSheet.BestShift(Sheet, ["E", "F#m", "B7"]).Should().Be(0);
        ChordSheet.BestShift(Sheet, []).Should().Be(0);
        ChordSheet.CapoFor(-2).Should().Be(2);
        // G D meets a song with A D E both at +2 (A E) and at -5 (D A); with capo 5 the capo's move wins.
        ChordSheet.BestShift("G   D", ["A", "D", "E"], capo: 5).Should().Be(-5);
        ChordSheet.BestShift("G   D", ["A", "D", "E"]).Should().Be(2);
    }

    [Test]
    public void A_song_keeps_its_pasted_sheet_and_moves_it_to_its_grips()
    {
        var now = new DateTime(2026, 10, 29, 18, 0, 0, DateTimeKind.Utc);
        var song = Song.Create("Test (Nogen)", now);
        song.SetCapo(2, now);
        song.UseChords(PartKind.Vers1, ["D", "Em7", "A7", "G"], null, now);

        song.PasteSheet(Sheet, now);
        song.SheetShift.Should().Be(-2);
        song.SheetLines[1].Text.Should().StartWith("D ");

        song.ShiftSheet(1, now);
        song.SheetLines[1].Text.Should().StartWith("Eb ");
        song.ShiftSheet(-7, now);
        song.SheetShift.Should().Be(4);    // -8 wraps to +4 instead of growing past half an octave

        var back = Song.Restore(song.Id, song.Title, song.Mood, song.Capo, song.Bpm, song.StrumId, song.Parts, song.CreatedAt, song.UpdatedAt, song.Sheet, song.SheetShift);
        back.Sheet.Should().Be(song.Sheet);
        back.SheetShift.Should().Be(4);

        song.ClearSheet(now);
        song.HasSheet.Should().BeFalse();
        FluentActions.Invoking(() => song.PasteSheet("  ", now)).Should().Throw<SongException>();
    }
}

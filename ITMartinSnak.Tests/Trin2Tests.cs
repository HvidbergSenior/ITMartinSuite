using FluentAssertions;
using ITMartinSnak.Domain;


namespace ITMartinSnak.Tests;

// Snak trin 2 (2026-11-12): several days + place, all file types up to 90 MB, reply/reactions/pictures/search in the chat.
public class Trin2Tests
{
    private static readonly DateTime Now = new(2026, 11, 12, 9, 0, 0, DateTimeKind.Utc);

    [Test]
    public void A_trip_over_several_days_is_on_every_day_of_it()
    {
        var t = GroupRules.NewTask([], "Kbh-tur", "", "", Now, day: "2026-11-04", endDay: "2026-11-07", place: "Kbh")!;
        t.EndDay.Should().Be("2026-11-07");
        t.Place.Should().Be("Kbh");
        GroupRules.IsOn(t, new DateOnly(2026, 11, 3)).Should().BeFalse();
        GroupRules.IsOn(t, new DateOnly(2026, 11, 4)).Should().BeTrue();
        GroupRules.IsOn(t, new DateOnly(2026, 11, 6)).Should().BeTrue();
        GroupRules.IsOn(t, new DateOnly(2026, 11, 7)).Should().BeTrue();
        GroupRules.IsOn(t, new DateOnly(2026, 11, 8)).Should().BeFalse();
        GroupRules.NextDay(t, new DateOnly(2026, 11, 5)).Should().Be("2026-11-05");
        GroupRules.NextDay(t, new DateOnly(2026, 11, 1)).Should().Be("2026-11-04");
    }

    [Test]
    public void A_last_day_before_the_first_or_a_yearly_day_has_no_span()
    {
        GroupRules.NewTask([], "x", "", "", Now, day: "2026-11-07", endDay: "2026-11-04")!.EndDay.Should().BeEmpty();
        GroupRules.NewTask([], "x", "", "", Now, day: "2026-04-12", yearly: true, endDay: "2026-04-14")!.EndDay.Should().BeEmpty();
        GroupRules.NewTask([], "x", "", "", Now, day: "2026-01-01", endDay: "2028-01-01")!.EndDay.Should().BeEmpty();
    }

    [Test]
    public void Reminders_of_a_multi_day_thing_come_on_the_first_day_only()
    {
        var t = GroupRules.NewTask([], "Kursus", "", "10:00", Now, day: "2026-11-12", endDay: "2026-11-13")!;
        GroupRules.DueReminders(t, new DateTime(2026, 11, 12, 9, 0, 0)).Should().Contain(60);
        GroupRules.DueReminders(t, new DateTime(2026, 11, 13, 9, 0, 0)).Should().BeEmpty();
    }

    [Test]
    public void Edit_keeps_or_clears_the_span_and_place()
    {
        var t = GroupRules.NewTask([], "Tur", "", "", Now, day: "2026-11-04", endDay: "2026-11-07", place: "Kbh")!;
        GroupRules.Edit(t, "Tur", "", "")!.EndDay.Should().Be("2026-11-07");
        GroupRules.Edit(t, "Tur", "", "", endDay: "", place: "")!.Should().Match<TaskItem>(x => x.EndDay == "" && x.Place == "");
    }

    [Test]
    public void Any_file_except_programs_and_scripts()
    {
        FileRules.Check("film.mkv", 50L * 1024 * 1024, 0).Should().BeNull();
        FileRules.TypeOf("film.mkv").Should().Be("application/octet-stream");
        FileRules.Check("noder.musicxml", 1000, 0).Should().BeNull();
        FileRules.Check("virus.exe", 1000, 0).Should().Contain("Programmer");
        FileRules.Check("side.html", 1000, 0).Should().NotBeNull();
        FileRules.Check("tegning.svg", 1000, 0).Should().NotBeNull();
        FileRules.Check("stor.mp4", FileRules.MaxSize + 1, 0).Should().Contain("90 MB");
        FileRules.Check("fuld.pdf", 1000, 0, FileRules.MaxRoomBytes).Should().Contain("fuldt");
    }

    [Test]
    public void A_reply_points_to_a_message_that_exists()
    {
        var first = GroupRules.NewMessage([], "Vibz", "Hej", Now)!;
        GroupRules.NewMessage([first], "Julius", "Hej selv", Now, replyTo: 1)!.ReplyTo.Should().Be(1);
        GroupRules.NewMessage([first], "Julius", "Hej selv", Now, replyTo: 99)!.ReplyTo.Should().BeNull();
    }

    [Test]
    public void A_picture_needs_no_text_but_a_plain_message_does()
    {
        GroupRules.NewMessage([], "Vibz", "", Now, fileId: 3)!.FileId.Should().Be(3);
        GroupRules.NewMessage([], "Vibz", "", Now).Should().BeNull();
    }

    [Test]
    public void A_reaction_toggles_and_only_the_six_are_allowed()
    {
        var m = GroupRules.NewMessage([], "Vibz", "Hej", Now)!;
        var thumbs = GroupRules.ReactionEmoji[0];
        var once = GroupRules.React(m, thumbs, "Julius")!;
        once.Reactions![thumbs].Should().Equal("Julius");
        GroupRules.React(once, thumbs, "julius")!.Reactions.Should().BeNull();
        GroupRules.React(m, "💩", "Julius").Should().BeNull();
        m.Reactions.Should().BeNull();   // the original is never changed
    }

    [Test]
    public void Search_finds_every_word_newest_first()
    {
        var list = new List<Message>();
        foreach (var (n, t) in new[] { ("Vibz", "Turboweekend fredag"), ("Julius", "Hvad med lørdag?"), ("Eigil", "Fredag er god til turboweekend") })
            list.Add(GroupRules.NewMessage(list, n, t, Now)!);
        GroupRules.Search(list, "turboweekend fredag").Select(m => m.Name).Should().Equal("Eigil", "Vibz");
        GroupRules.Search(list, "julius").Should().ContainSingle();
        GroupRules.Search(list, "a").Should().BeEmpty();
    }
}

using FluentAssertions;
using ITMartinKanal.Application;
using ITMartinKanal.Domain;

namespace ITMartinKanal.Tests;

// Kanal (demo 2026-10-30): anyone may write; phone numbers and e-mails are taken out; newest first; a daily cap.
public class KanalTests
{
    private static readonly KanalChannel Pelle = new("pelle", "Datacentre og grøn energi", "", ["Datacentre", "Grøn energi"]);
    private static readonly DateTime Now = new(2026, 10, 30, 12, 0, 0, DateTimeKind.Utc);

    private sealed class MemoryStore : IKanalStore
    {
        public KanalData Data = new();
        public KanalData Get() => Data;
        public void Update(Action<KanalData> change) => change(Data);
    }

    [Test]
    public void Phone_numbers_and_emails_are_taken_out()
    {
        var c = KanalComment.Write(Pelle, "Grøn energi", "Ole", "Ring på 31 19 47 30 eller skriv til a@b.dk", Now);
        c.Text.Should().Be("Ring på [skjult] eller skriv til [skjult]");
        c.Topic.Should().Be("Grøn energi");
    }

    [Test]
    public void An_unknown_topic_falls_back_and_an_empty_text_is_refused()
    {
        KanalComment.Write(Pelle, "Krig", "", "Hej", Now).Topic.Should().Be("Datacentre");
        var act = () => KanalComment.Write(Pelle, "", "", "   ", Now);
        act.Should().Throw<ArgumentException>();
    }

    [Test]
    public void Writes_show_newest_first_and_only_in_their_own_channel()
    {
        var store = new MemoryStore();
        var now = Now;
        var kanal = new KanalUseCases(store, new KanalChannels([Pelle]), () => now);
        kanal.Write("pelle", "Datacentre", "", "Første").Should().BeNull();
        now = Now.AddMinutes(5);
        kanal.Write("pelle", "Grøn energi", "Mette", "Anden").Should().BeNull();
        kanal.Write("ukendt", "", "", "x").Should().Be("Kanalen findes ikke.");
        kanal.Comments("pelle").Select(c => c.Text).Should().Equal("Anden", "Første");
    }

    [Test]
    public void A_flood_is_stopped_at_the_daily_cap()
    {
        var store = new MemoryStore();
        for (var i = 0; i < KanalRules.MaxPerDay; i++) store.Data.Comments.Add(KanalComment.Write(Pelle, "", "", "x" + i, Now.AddMinutes(-i)));
        new KanalUseCases(store, new KanalChannels([Pelle]), () => Now).Write("pelle", "", "", "en til").Should().StartWith("Der er skrevet meget");
    }

    [Test]
    public void The_page_sleeps_from_23_to_7_Danish_time_and_takes_no_comments()
    {
        KanalRules.Asleep(new DateTime(2026, 10, 30, 22, 59, 0)).Should().BeFalse();
        KanalRules.Asleep(new DateTime(2026, 10, 30, 23, 0, 0)).Should().BeTrue();
        KanalRules.Asleep(new DateTime(2026, 10, 31, 6, 59, 0)).Should().BeTrue();
        KanalRules.Asleep(new DateTime(2026, 10, 31, 7, 0, 0)).Should().BeFalse();
        var night = new DateTime(2026, 10, 30, 23, 30, 0, DateTimeKind.Utc);   // 00:30 Danish time
        new KanalUseCases(new MemoryStore(), new KanalChannels([Pelle]), () => night).Write("pelle", "", "", "Hej").Should().StartWith("Siden sover");
    }
}

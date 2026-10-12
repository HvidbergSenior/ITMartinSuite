using System.Xml.Linq;
using FluentAssertions;
using ITMartinDailyBrief.Application;
using ITMartinDailyBrief.Domain;
using ITMartinDailyBrief.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;

namespace ITMartinDailyBrief.Tests;

public class NewsTests
{
    private static readonly DateTime T0 = new(2026, 10, 12, 7, 0, 0, DateTimeKind.Utc);

    private sealed class Clock(DateTime now) : TimeProvider
    {
        public DateTime Now = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private static NewsItem Item(string url, int minutesAgo, string source = "DR") =>
        new($"Titel {url}", url, "", "", "", source, NewsSource.Grey, T0.AddMinutes(-minutesAgo));

    private sealed class Reader : IFeedReader
    {
        public int Calls;
        public Dictionary<string, NewsItem[]> Items = [];
        public Task<IReadOnlyList<NewsItem>> ReadAsync(NewsSource source, CancellationToken ct)
        {
            Calls++;
            return Task.FromResult<IReadOnlyList<NewsItem>>(Items.GetValueOrDefault(source.Id, []));
        }
    }

    private sealed class Writer : ISummaryWriter
    {
        public int Calls;
        public string Answer = "Kort fortalt.";
        public Task<string> SummarizeAsync(NewsItem item, CancellationToken ct) { Calls++; return Task.FromResult(Answer); }
    }

    [Test]
    public void Front_page_is_newest_first_without_doubles_and_ends_at_the_readers_number()
    {
        var page = NewsItem.FrontPage([[Item("a", 30), Item("b", 5)], [Item("c", 10), Item("a", 30)]], 2);
        page.Select(i => i.Url).Should().Equal("b", "c");
    }

    [Test]
    public void Choice_defaults_to_ten_and_all_presets_on_and_refuses_odd_counts()
    {
        var c = new ReaderChoice(maxItems: 7);
        c.MaxItems.Should().Be(10);
        c.ActiveSources.Should().HaveCount(NewsSource.Presets.Length);
        c.SetCount(15);
        c.SetCount(1000);
        c.MaxItems.Should().Be(15);
    }

    [Test]
    public void Own_sources_must_be_web_addresses_and_can_be_switched_and_removed()
    {
        var c = new ReaderChoice(enabled: new Dictionary<string, bool> { ["bt"] = false });
        c.AddCustom("Lokal", "javascript:alert(1)").Should().BeFalse();
        c.AddCustom("", "https://x.dk/rss").Should().BeFalse();
        c.AddCustom("Zetland", "https://zetland.dk/rss").Should().BeTrue();
        var mine = c.CustomSources.Single();
        c.ActiveSources.Should().Contain(mine).And.NotContain(s => s.Id == "bt");
        c.Switch(mine, false);
        c.ActiveSources.Should().NotContain(mine);
        c.RemoveCustom(mine.Id);
        c.AllSources.Should().HaveCount(NewsSource.Presets.Length);
    }

    [Test]
    public async Task Each_source_is_read_once_a_minute_and_articles_can_be_found_afterwards()
    {
        var reader = new Reader { Items = { ["dr"] = [Item("x", 3)] } };
        var clock = new Clock(T0);
        var feed = new NewsFeed(reader, clock);
        var only = new ReaderChoice(enabled: NewsSource.Presets.ToDictionary(p => p.Id, p => p.Id == "dr"));
        (await feed.FrontPageAsync(only)).Should().ContainSingle();
        await feed.FrontPageAsync(only);
        reader.Calls.Should().Be(1);
        clock.Now = T0.AddMinutes(2);
        await feed.FrontPageAsync(only);
        reader.Calls.Should().Be(2);
        feed.Find(Item("x", 3).Id)!.Url.Should().Be("x");
        feed.Find("nope").Should().BeNull();
    }

    [Test]
    public async Task Summaries_are_made_once_per_article_and_stop_at_the_day_limit()
    {
        var writer = new Writer();
        var clock = new Clock(T0);
        var ai = new Summaries(writer, new SummaryBudget(2), clock, NullLogger<Summaries>.Instance);
        (await ai.SummarizeAsync(Item("a", 1))).Outcome.Should().Be(Summaries.Outcome.Ok);
        (await ai.SummarizeAsync(Item("a", 1))).Text.Should().Be("Kort fortalt.");
        writer.Calls.Should().Be(1);
        (await ai.SummarizeAsync(Item("b", 1))).Outcome.Should().Be(Summaries.Outcome.Ok);
        (await ai.SummarizeAsync(Item("c", 1))).Outcome.Should().Be(Summaries.Outcome.DayLimit);
        writer.Calls.Should().Be(2);
        clock.Now = T0.AddDays(1);
        (await ai.SummarizeAsync(Item("c", 1))).Outcome.Should().Be(Summaries.Outcome.Ok);
        (await ai.SummarizeAsync(null)).Outcome.Should().Be(Summaries.Outcome.NoArticle);
    }

    [Test]
    public void Rss_items_get_text_without_html_plus_picture_and_sound()
    {
        var doc = XDocument.Parse("""
            <rss xmlns:media="http://search.yahoo.com/mrss/"><channel>
              <item><title> Storm over Sjælland </title><link>https://dr.dk/1</link>
                <description>&lt;p&gt;Det &lt;b&gt;blæser&lt;/b&gt; &amp;amp; regner&lt;/p&gt;</description>
                <pubDate>Mon, 12 Oct 2026 06:30:00 GMT</pubDate>
                <media:thumbnail url="https://dr.dk/1.jpg" />
                <enclosure url="https://dr.dk/1.mp3" type="audio/mpeg" /></item>
              <item><title></title><link>https://dr.dk/empty</link></item>
            </channel></rss>
            """);
        var items = RssFeedReader.Parse(doc, NewsSource.Presets.Single(p => p.Id == "dr"), T0);
        var i = items.Single();
        i.Title.Should().Be("Storm over Sjælland");
        i.Description.Should().Be("Det blæser & regner");
        i.ImageUrl.Should().Be("https://dr.dk/1.jpg");
        i.HasAudio.Should().BeTrue();
        i.TimeAgo(T0).Should().Be("30 min");
    }

    [Test]
    public void Atom_feeds_work_too()
    {
        var doc = XDocument.Parse("""
            <feed xmlns="http://www.w3.org/2005/Atom"><entry><title>Atom-nyhed</title><link href="https://x.dk/a"/>
            <updated>2026-10-12T05:00:00Z</updated><summary>Kort</summary></entry></feed>
            """);
        RssFeedReader.Parse(doc, new NewsSource("x", "X", "https://x.dk/feed"), T0).Single().Url.Should().Be("https://x.dk/a");
    }
}

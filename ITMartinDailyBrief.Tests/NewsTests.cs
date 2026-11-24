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
        public ArticleText? Got;
        public Task<string> SummarizeAsync(NewsItem item, ArticleText article, CancellationToken ct) { Calls++; Got = article; return Task.FromResult(Answer); }
    }

    private sealed class Pages(string text = "") : IArticleTextReader
    {
        public Task<ArticleText> ReadAsync(NewsItem item, CancellationToken ct) => Task.FromResult(new ArticleText(text));
    }

    [Test]
    public void Front_page_is_newest_first_without_doubles_and_ends_at_the_readers_number()
    {
        var page = NewsItem.FrontPage([[Item("a", 30), Item("b", 5)], [Item("c", 10), Item("a", 30)]], 2);
        page.Select(i => i.Url).Should().Equal("b", "c");
    }

    [Test]
    public void Choice_defaults_to_25_and_all_presets_on_and_refuses_odd_counts()
    {
        // 25 latest as default since 2026-11-24 (user: "able to see like 25 latest news").
        var c = new ReaderChoice(maxItems: 7);
        c.MaxItems.Should().Be(25);
        c.ActiveSources.Should().HaveCount(NewsSource.Presets.Length);
        c.SetCount(15);
        c.SetCount(1000);
        c.MaxItems.Should().Be(15);
        c.SetCount(25);
        c.MaxItems.Should().Be(25);
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
        var ai = new Summaries(writer, new Pages(), new SummaryBudget(2), clock, NullLogger<Summaries>.Instance);
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

    private static NewsItem News(string title, string desc = "") =>
        new(title, "u-" + title, desc, "", "", "DR", NewsSource.Grey, T0);

    [Test]
    public void Topics_keep_only_matching_news_and_a_surname_is_enough()
    {
        var f = TopicFilter.Parse("Donald Trump, ukraine");
        f.Topics.Should().Equal("Donald Trump", "ukraine");
        f.Matches(News("Trumps nye toldsatser rammer Danmark")).Should().BeTrue();
        f.Matches(News("Donald Trump taler i Kongressen")).Should().BeTrue();
        f.Matches(News("Vejret", "Krigen i Ukraine fylder i EU")).Should().BeTrue();
        f.Matches(News("Atrumpet er ikke et ord")).Should().BeFalse();
        f.Matches(News("Storm over Sjælland")).Should().BeFalse();
        new TopicFilter().Matches(News("Hvad som helst")).Should().BeTrue();
        TopicFilter.Parse(" ,  ,").IsEmpty.Should().BeTrue();
        TopicFilter.Parse(string.Join(",", Enumerable.Range(1, 20).Select(i => $"emne{i}"))).Topics.Should().HaveCount(TopicFilter.MaxTopics);
    }

    [Test]
    public void Front_page_with_a_topic_still_ends_at_the_readers_number()
    {
        var items = new[] { News("Trump 1"), News("Storm"), News("Trump 2"), News("Trump 3") };
        NewsItem.FrontPage([items], 2, TopicFilter.Parse("Trump")).Should().HaveCount(2).And.OnlyContain(i => i.Title.StartsWith("Trump"));
        var c = new ReaderChoice();
        c.SetTopics("Trump");
        c.Topics.AsText.Should().Be("Trump");
    }

    [Test]
    public void Summary_text_is_read_from_the_text_block_whatever_comes_first()
    {
        ClaudeSummaryWriter.TextOf("""{"content":[{"type":"thinking","thinking":"..."},{"type":"text","text":" Kort fortalt. "}]}""")
            .Should().Be("Kort fortalt.");
        ClaudeSummaryWriter.TextOf("""{"content":[]}""").Should().BeEmpty();
        ClaudeSummaryWriter.TextOf("ikke json").Should().BeEmpty();
    }

    [Test]
    public async Task Summary_uses_the_whole_article_when_it_can_be_read_and_says_so()
    {
        var whole = new string('x', ArticleText.MinWhole);
        var writer = new Writer();
        var ai = new Summaries(writer, new Pages(whole), new SummaryBudget(5), new Clock(T0), NullLogger<Summaries>.Instance);
        var r = await ai.SummarizeAsync(Item("a", 1));
        r.FromWholeArticle.Should().BeTrue();
        writer.Got!.Text.Should().Be(whole);
        ai.Known(Item("a", 1).Id)!.FromWholeArticle.Should().BeTrue();

        var locked = new Summaries(writer, new Pages("Kun en kort teaser."), new SummaryBudget(5), new Clock(T0), NullLogger<Summaries>.Instance);
        (await locked.SummarizeAsync(Item("b", 1))).FromWholeArticle.Should().BeFalse();
        writer.Got!.Should().Be(ArticleText.None);
    }

    [Test]
    public void Article_text_is_the_body_paragraphs_without_menus_scripts_or_short_bits()
    {
        var body = "Regeringen fremlagde i dag et nyt forslag om skat på pension, som skal gælde fra næste år.";
        var html = $"""
            <html><body><nav><p>Forside · Nyheder · Sport · Vejret · Kontakt · Om os · Abonnement</p></nav>
            <article><h1>Titel</h1><p>{body}</p><p>Kort</p><script>var x = "hemmelig kode der ikke er tekst";</script>
            <p>Forslaget får blandet modtagelse hos oppositionen, der kalder det &amp; urimeligt for mange.</p>
            <p>Skatteministeren siger, at forslaget vil give flere penge til velfærd, og at pensionisterne med de laveste indtægter slipper.</p></article>
            <article><h2>Storm over Sjælland</h2><p>Det blæser kraftigt over hele Sjælland i dag, og flere færger er aflyst resten af dagen.</p></article>
            <footer><p>Copyright og cookies og alt det der står nederst på siden hver gang.</p></footer></body></html>
            """;
        var text = HtmlArticleReader.Extract(html, "Nyt forslag om skat på pension").Text;
        HtmlArticleReader.Extract(html, "Storm lukker Storebæltsbroen").IsWhole.Should().BeFalse();
        text.Should().Contain(body).And.Contain("& urimeligt").And.NotContain("Storm").And.NotContain("Forside").And.NotContain("Copyright").And.NotContain("hemmelig");
        HtmlArticleReader.Extract("<html><body></body></html>", "x").IsWhole.Should().BeFalse();
    }

    [Test]
    public void Articles_are_never_fetched_from_the_home_network()
    {
        foreach (var ip in new[] { "10.0.0.200", "192.168.1.1", "127.0.0.1", "172.16.0.5", "169.254.1.1", "100.64.0.1", "::1", "fd00::1" })
            HtmlArticleReader.IsPublic(System.Net.IPAddress.Parse(ip)).Should().BeFalse(ip);
        HtmlArticleReader.IsPublic(System.Net.IPAddress.Parse("151.101.1.1")).Should().BeTrue();
    }

    [Test]
    public void Prompt_only_asks_for_a_long_summary_when_the_whole_article_is_there()
    {
        var item = new NewsItem("Storm", "u", "Det blæser.", "", "", "DR", NewsSource.Grey, T0);
        ClaudeSummaryWriter.Prompt(item, new ArticleText(new string('y', ArticleText.MinWhole))).Should().Contain("150-200 ord").And.Contain("Artiklens tekst");
        ClaudeSummaryWriter.Prompt(item, ArticleText.None).Should().Contain("2-3").And.Contain("Det blæser.").And.NotContain("150-200");
    }
}

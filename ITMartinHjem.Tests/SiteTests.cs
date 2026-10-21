using FluentAssertions;
using ITMartinHjem.Application;
using ITMartinHjem.Domain;

namespace ITMartinHjem.Tests;

// The site rules that moved out of the flat Server 2026-10-20.
public class SiteTests
{
    [Test]
    public void A_day_entry_keeps_its_line_breaks_and_the_oldest_fall_off()
    {
        var s = new Settings();
        var at = new DateTime(2026, 10, 20, 9, 30, 0);
        s.SetEntries([new("a.jpg", "a_t.jpg", at, "Første linje\nAnden linje")]).Should().BeEmpty();
        s.NowPhotos.Should().NotContain("\n");   // one entry = one stored line
        s.Entries().Single().Text.Should().Be("Første linje\nAnden linje");

        var many = Enumerable.Range(0, Settings.MaxDayEntries + 2).Select(i => new Settings.DayEntry($"{i}.jpg", "", at.AddMinutes(i), "")).ToList();
        var gone = s.SetEntries(many);
        gone.Select(e => e.Stored).Should().BeEquivalentTo(["0.jpg", "1.jpg"]);
        s.Entries().Should().HaveCount(Settings.MaxDayEntries);
    }

    [Test]
    public void Only_files_the_front_page_uses_are_public_under_nu()
    {
        var s = new Settings { NowMediaName = "film.mp4", NowMediaType = "video/mp4", NowMediaThumb = "film_t.jpg" };
        s.SetEntries([new("dag.jpg", "dag_t.jpg", DateTime.Now, "")]);
        s.ContentTypeOfNowFile("film.mp4").Should().Be("video/mp4");
        s.ContentTypeOfNowFile("film_t.jpg").Should().Be("image/jpeg");
        s.ContentTypeOfNowFile("dag_t.jpg").Should().Be("image/jpeg");
        s.ContentTypeOfNowFile("other.jpg").Should().BeNull();
        s.ContentTypeOfNowFile("").Should().BeNull();
    }

    [Test]
    public void Page_addresses_are_cleaned_and_the_apps_own_are_refused()
    {
        PageRules.NormalizeSlug(" /Om Mig/ ").Should().Be("om-mig");
        PageRules.IsReserved("admin").Should().BeTrue();
        PageRules.IsReserved("api/x").Should().BeTrue();
        PageRules.IsReserved("om-mig").Should().BeFalse();
        PageRules.ParentOf("ydelser/polstring").Should().Be("ydelser");
    }

    [Test]
    public void The_menu_starts_with_Forside_and_slots_in_the_fixed_tabs()
    {
        var menu = SiteMenu.Build([new Page { Title = "Om mig", Slug = "om-mig", Sort = 45 }]);
        menu.Select(i => i.Title).Should().Equal("Forside", "I dag", "Om mig", "Mine apps", "Spørgsmål");
    }

    [Test]
    public void The_visitor_is_told_90_s_after_the_last_reply_and_only_when_reachable()
    {
        var now = DateTime.UtcNow;
        var t = new ChatThread { VisitorEmail = "a@b.dk", Messages = [new() { FromOwner = true, Text = "Hej", At = now.AddSeconds(-30) }] };
        t.VisitorIsDue(now).Should().BeFalse();
        t.VisitorIsDue(now.AddSeconds(61)).Should().BeTrue();
        t.VisitorEmail = "";
        t.VisitorIsDue(now.AddSeconds(61)).Should().BeFalse();
    }

    [Test]
    public void Martin_gets_a_mail_copy_of_an_unread_message_after_2_minutes()
    {
        var now = DateTime.UtcNow;
        var t = new ChatThread { UnreadForOwner = true, Messages = [new() { Text = "Er du der?", At = now }] };
        t.OwnerCopyIsDue(now.AddMinutes(1)).Should().BeFalse();
        t.OwnerCopyIsDue(now.AddMinutes(2)).Should().BeTrue();
        t.OwnerMailedAt = now.AddMinutes(2);
        t.OwnerCopyIsDue(now.AddMinutes(3)).Should().BeFalse();
    }

    [Test]
    public void A_hit_is_counted_only_for_our_sites_and_never_for_bots()
    {
        var req = new HitRequest("/?ref=facebook&fbclid=x", "Forside", null, "abc", "www.itmartin.dk");
        var hit = VisitRules.ToHit(req, "Mozilla/5.0 (iPhone) Mobile")!;
        hit.Host.Should().Be("itmartin.dk");
        hit.Path.Should().Be("/");
        hit.Source.Should().Be("Facebook");
        hit.Device.Should().Be("mobil");
        VisitRules.ToHit(req, "Googlebot/2.1").Should().BeNull();
        VisitRules.ToHit(req with { Host = "example.com" }, "Mozilla/5.0").Should().BeNull();
        VisitRules.DomainOf("lager.bogshoppen.dk").Should().Be("bogshoppen.dk");
    }
}

// The layer rules (CLAUDE.md): Domain knows nothing outside itself, Application only Domain (+ the shared shop words),
// and no page talks to the database or injects a port.
public class LayerTests
{
    private const string SharedWords = "ITMartinVarer.Contracts";
    private static readonly string Server = Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "ITMartinHjem.Server");

    private static string[] Refs(Type t) => t.Assembly.GetReferencedAssemblies().Select(a => a.Name!)
        .Where(n => n.StartsWith("ITMartin") || n.StartsWith("Microsoft.EntityFramework") || n.StartsWith("Anthropic")).ToArray();

    [Test]
    public void Domain_has_no_outside_dependencies() => Refs(typeof(Settings)).Should().BeEmpty();

    [Test]
    public void Application_depends_only_on_Domain() =>
        Refs(typeof(SiteUseCases)).Should().BeSubsetOf(["ITMartinHjem.Domain", SharedWords]);

    [Test]
    public void No_page_uses_the_database_infrastructure_or_a_port()
    {
        var files = Directory.GetFiles(Server, "*.razor", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")).ToList();
        files.Should().NotBeEmpty();
        foreach (var file in files)
            File.ReadAllText(file).Should().NotContain("DbContext", file).And.NotContain("HjemDb", file).And.NotContain("Infrastructure", file)
                .And.NotMatchRegex(@"@inject I[A-Z]\w*(Gateway|Repository|Store|Sender|Checker|Status)\b", file);
    }

    [Test]
    public void The_server_has_no_database_package() =>
        File.ReadAllText(Path.Combine(Server, "ITMartinHjem.Server.csproj")).Should().NotContain("EntityFrameworkCore");
}

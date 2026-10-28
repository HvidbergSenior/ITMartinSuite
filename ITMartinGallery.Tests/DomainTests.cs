using System.Text.Json.Nodes;
using FluentAssertions;
using ITMartinGallery.Domain;

namespace ITMartinGallery.Tests;

public class DomainTests
{
    private static Gallery G(string slug = "fam", string path = "/lib", string? password = null) =>
        new(slug, "Familien", path, password, false, false, false, false, false, false);

    [TestCase("01-January", "01-Januar")]
    [TestCase("05-May - 2. halvdel", "05-Maj - 2. halvdel")]
    [TestCase("3 Marts", "3 Marts")]
    [TestCase("Ukendt måned", "Ukendt måned")]
    public void Month_folders_are_shown_in_Danish(string raw, string shown) => FolderNames.DanishFolderName(raw).Should().Be(shown);

    [TestCase("05-May - 1. halvdel", "05-Maj")]
    [TestCase("10 November", "10-November")]
    [TestCase("05-12 Maj", "05-12 Maj")]
    [TestCase("Jan-Mar", "Jan-Mar")]
    [TestCase("Ukendt måned", null)]
    public void Both_halves_of_a_month_share_one_label(string raw, string? label) => FolderNames.MonthLabelFor(raw).Should().Be(label);

    [Test]
    public void Months_sort_in_calendar_order() =>
        new[] { "10 November", "1 Januar", "2 Februar", "Okt-Dec" }.OrderBy(FolderNames.MonthSortKeyFor)
            .Should().Equal("1 Januar", "2 Februar", "10 November", "Okt-Dec");

    [Test]
    public void A_gallery_without_password_admits_everyone_and_one_with_admits_only_the_password()
    {
        G().Admits(null).Should().BeTrue();
        G(password: "pw").Admits("pw").Should().BeTrue();
        G(password: "pw").Admits("x").Should().BeFalse();
        G(password: "pw").Admits(null).Should().BeFalse();
    }

    [Test]
    public void Edit_needs_its_own_pin() =>
        (G() with { EditPin = "1234" }).AllowsEdit("pw").Should().BeFalse();

    [Test]
    public void Screenshots_are_hidden_only_where_asked()
    {
        G().HiddenRootFolders.Should().NotContain("Screenshots");
        (G() with { HideScreenshots = true }).HiddenRootFolders.Should().Contain("Screenshots");
        FolderNames.RootFoldersAlwaysHidden.Should().NotContain("Screenshots");
    }

    [Test]
    public void RootFolders_limits_the_view_only_front_page_and_MusicName_renames_the_music_tile()
    {
        var g = G() with { RootFolders = "Billeder, Videoer", MusicName = "Bents Korsange" };
        g.ShowAtRoot("Videoer").Should().BeTrue();
        g.ShowAtRoot("SmartFolders/People").Should().BeFalse();
        g.RootName(FolderNames.ViewOnlyRootFolders.Single(v => v.Rel == "Musik")).Should().Be("Bents Korsange");
    }

    [Test]
    public void Catalog_serves_only_entries_with_a_path_but_closes_them_all()
    {
        var catalog = new GalleryCatalog([G("Fam"), G("gone", path: "") with { ClosesAt = new DateTime(2020, 1, 1) }]);
        catalog.Find("fam").Should().BeNull();
        catalog.FindIgnoreCase("fam").Should().NotBeNull();
        catalog.Find("gone").Should().BeNull();
        catalog.ClosingTimes.Should().ContainKey("gone");
    }

    [Test]
    public void Song_match_prefers_same_folder_and_file()
    {
        SongMatch.Score(@"C:\x\Album\song.mp3", "Sang", "song.mp3", "Album").Should().Be(3);
        SongMatch.Score("C:/y/Other/song.mp3", "Sang", "song.mp3", "Album").Should().Be(2);
        SongMatch.Score("C:/x/Album/take2.mp3", "Sang", "song.mp3", "Album").Should().Be(1);
        SongMatch.Score("C:/x/z/take2.mp3", "Sang", "song.mp3", "Album").Should().Be(0);
    }

    [Test]
    public void Celebration_edits_merge_per_entry_and_clean_text()
    {
        var edits = new JsonObject();
        CelebrationRules.ApplyEdit(edits, "caption", "a.jpg", null, "  Hej  ", null);
        CelebrationRules.ApplyEdit(edits, "rotate", "a.jpg", null, null, -90);
        CelebrationRules.ApplyEdit(edits, "year", "2005", "T", new string('x', 700), null);
        var texts = CelebrationRules.Merge(JsonNode.Parse("""{"captions":{"b.jpg":"B"},"intro":"i"}""")!.AsObject(), edits);
        texts["captions"]!["a.jpg"]!.GetValue<string>().Should().Be("Hej");
        texts["captions"]!["b.jpg"]!.GetValue<string>().Should().Be("B");
        texts["rotate"]!["a.jpg"]!.GetValue<int>().Should().Be(270);
        texts["years"]!["2005"]!["text"]!.GetValue<string>().Length.Should().Be(600);
        FluentActions.Invoking(() => CelebrationRules.ApplyEdit(edits, "field", "evil", null, "x", null)).Should().Throw<ArgumentException>();
    }

    [Test]
    public void Celebration_hides_skipped_and_too_early_pictures()
    {
        var items = new[]
        {
            new CelebrationRules.Item("old.jpg", "img", "u", null, new DateTime(1999, 1, 2)),
            new CelebrationRules.Item("scan.jpg", "img", "u", null, null),
            new CelebrationRules.Item("ok.jpg", "img", "u", null, new DateTime(2005, 1, 2)),
            new CelebrationRules.Item("undated.jpg", "img", "u", null, null),
        };
        var texts = JsonNode.Parse("""{"fromYear":2000,"skip":["SCAN.jpg"]}""")!.AsObject();
        CelebrationRules.Shown(items, texts).Select(i => i.Name).Should().Equal("ok.jpg", "undated.jpg");
    }

    [TestCase("IMG_20050601_101010.jpg", "2005-06-01")]
    [TestCase("20100101_000000.jpg", null)]   // a flat clock battery's 1 January midnight
    [TestCase("scan.jpg", null)]
    public void Dates_come_from_file_names_when_plausible(string name, string? date) =>
        CelebrationRules.DateFromName(name).Should().Be(date is null ? null : DateTime.Parse(date));

    [Test]
    public void Closing_warning_names_the_contact() =>
        ClosingRule.Warning(new DateTime(2099, 1, 1, 10, 0, 0)).Should().Contain(ClosingRule.Contact).And.Contain("kl. 10.00");
}

public class LibraryPathTests
{
    [Test]
    public void Only_the_library_and_what_is_inside_it_is_safe()
    {
        var root = Path.Combine(Path.GetTempPath(), "anna");
        LibraryPaths.IsSafe(root, root).Should().BeTrue();
        LibraryPaths.IsSafe(root + Path.DirectorySeparatorChar, root).Should().BeTrue();
        LibraryPaths.IsSafe(Path.Combine(root, "Images", "a.jpg"), root).Should().BeTrue();
        LibraryPaths.IsSafe(root + "-old", root).Should().BeFalse();
        LibraryPaths.IsSafe(Path.Combine(root, "..", "other"), root).Should().BeFalse();
    }
}

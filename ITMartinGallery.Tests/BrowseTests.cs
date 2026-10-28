using FluentAssertions;
using ITMartinGallery.Application;
using ITMartinGallery.Domain;
using ITMartinGallery.Infrastructure;

namespace ITMartinGallery.Tests;

// The browse use case against a small library on the real disk adapter.
public class BrowseTests
{
    private string _root = "";
    private FolderBrowser _browser = null!;

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), "gallery-test-" + Guid.NewGuid().ToString("N"));
        var disk = new LibraryDisk();
        _browser = new FolderBrowser(disk, new LibraryLinks(disk), new PeopleIndex(disk));
    }

    [TearDown]
    public void TearDown() => Directory.Delete(_root, true);

    private void Put(string rel, DateTime? written = null)
    {
        var full = Path.Combine(_root, rel);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, "x");
        File.SetLastWriteTimeUtc(full, written ?? new DateTime(2024, 1, 1));
    }

    private Gallery G(bool viewOnly = false) => new("fam", "Familien", _root, null, false, false, false, false, false, false, viewOnly);

    private BrowsePage Browse(string? path, bool viewOnly = false)
    {
        var answer = _browser.Browse(G(viewOnly), path);
        answer.Outcome.Should().Be(Outcome.Ok);
        return answer.Value!;
    }

    [Test]
    public void Root_leads_with_pictures_and_hides_working_folders()
    {
        Put("Images/2024/a.jpg"); Put("Videos/v.mp4"); Put("Duplicates/d.jpg"); Put("Memes/m.gif"); Put("Empty/notes.txt"); Put("@eaDir/x.jpg");
        Browse(null).Folders.Select(f => f.name).Should().Equal("Billeder", "Videoer", "Memes");
    }

    [Test]
    public void A_year_of_only_month_folders_is_one_view_in_calendar_order()
    {
        Put("Images/2024/10-October/c.jpg"); Put("Images/2024/01-January/a.jpg"); Put("Images/2024/2 Februar/b.jpg");
        var page = Browse("Images/2024");
        page.Folders.Should().BeEmpty();
        page.Files.Select(f => f.MonthLabel).Should().Equal("01-Januar", "2-Februar", "10-Oktober");
    }

    [Test]
    public void Loose_photos_next_to_folders_get_an_Ukendt_dato_card_that_opens()
    {
        Put("Images/2023/3 Marts/e.jpg"); Put("Images/2023/Andet/f.jpg"); Put("Images/2023/loose.jpg");
        var page = Browse("Images/2023");
        page.Files.Should().BeEmpty();
        page.Folders.Last().relPath.Should().Be("Images/2023/Ukendt dato");

        var loose = Browse("Images/2023/Ukendt dato");
        loose.Title.Should().Be("Ukendt dato");
        loose.ParentRelPath.Should().Be("Images/2023");
        loose.Files.Select(f => f.Name).Should().Equal("loose.jpg");
    }

    [Test]
    public void Inside_Musik_only_the_music_shows()
    {
        Put("Musik/Album/song.mp3"); Put("Musik/Album/Folder.jpg");
        var file = Browse("Musik/Album").Files.Single();
        file.Name.Should().Be("song.mp3");
        file.Thumb.Should().StartWith("/api/embedded-cover?gallery=fam&path=Musik%2FAlbum%2Fsong.mp3");
    }

    [Test]
    public void View_only_front_page_has_the_fixed_tiles_and_curated_albums()
    {
        Put("Billeder/p.jpg"); Put("SmartFolders/People/Bo/p.jpg"); Put("SmartFolders/Trøstebog Evia/t.jpg"); Put("SmartFolders/BedsteBillede/b.jpg");
        Browse(null, viewOnly: true).Folders.Select(f => f.name).Should().Equal("Billeder", "Personer", "Trøstebog Evia");
        Browse("SmartFolders/People", viewOnly: true).ParentRelPath.Should().Be("");
    }

    [Test]
    public void A_path_outside_the_library_is_refused()
    {
        Put("Images/a.jpg");
        Directory.CreateDirectory(_root + "-other");
        try { _browser.Browse(G(), "../" + Path.GetFileName(_root) + "-other").Outcome.Should().Be(Outcome.BadRequest); }
        finally { Directory.Delete(_root + "-other"); }
        _browser.Browse(G(), "Nope").Outcome.Should().Be(Outcome.NotFound);
    }
}

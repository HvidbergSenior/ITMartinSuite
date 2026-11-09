using FluentAssertions;
using ITMartinHjemmeside.Application;
using ITMartinHjemmeside.Domain;
using ITMartinHjemmeside.Infrastructure;
using ITMartinHjemmeside.Server;

namespace ITMartinHjemmeside.Tests;

public class HjemmesideTests
{
    private string _dir = "";
    private SiteFolders _folders = null!;
    private Sites _sites = null!;
    private Editor _editor = null!;

    [SetUp]
    public void Setup()
    {
        _dir = Path.Combine(Path.GetTempPath(), "hj-test-" + Guid.NewGuid().ToString("N"));
        _folders = new SiteFolders(_dir);
        _sites = new Sites(_folders, _folders, TimeProvider.System);
        _editor = new Editor(_folders, _folders, TimeProvider.System);
    }

    [TearDown]
    public void TearDown() { try { Directory.Delete(_dir, true); } catch { } }

    [Test]
    public void New_site_comes_with_front_about_and_contact()
    {
        var s = _sites.Create("Skovbunden", null, Package.Selv, "hemmelig1", "www.Skovbunden.dk/");
        s.Id.Should().Be("skovbunden");
        s.Pages.Select(p => p.Slug).Should().Equal("forside", "om-os", "kontakt");
        s.Hosts.Should().Equal("www.skovbunden.dk");
        _sites.ByHost("skovbunden.dk")!.Id.Should().Be("skovbunden");
        _sites.ByHost("www.skovbunden.dk:443")!.Id.Should().Be("skovbunden");
        _sites.ByHost("hjemmeside.itmartin.dk").Should().BeNull();
    }

    [Test]
    public void Short_name_and_domain_can_only_be_used_once()
    {
        _sites.Create("Skovbunden", null, Package.Selv, null, "skovbunden.dk");
        FluentActions.Invoking(() => _sites.Create("Skovbunden", null, Package.Selv, null, null)).Should().Throw<SiteException>();
        var other = _sites.Create("Andet", null, Package.Selv, null, null);
        FluentActions.Invoking(() => _sites.SetUp(other.Id, "Andet", "", "", "", "", ["skovbunden.dk"], Package.Selv, null, "", null))
            .Should().Throw<SiteException>().WithMessage("*Skovbunden*");
    }

    [Test]
    public void Undo_takes_one_step_back_each_time()
    {
        var s = _sites.Create("Test", null, Package.Selv, null, null);
        var block = s.Pages[0].Blocks[0].Id;
        _editor.EditText(s.Id, block, "Første");
        _editor.EditText(s.Id, block, "Anden");
        _editor.UndoSteps(s.Id).Should().Be(2);

        _editor.Undo(s.Id).Should().BeTrue();
        _sites.Get(s.Id)!.Pages[0].Blocks[0].Text.Should().Be("Første");
        _editor.Undo(s.Id).Should().BeTrue();
        _sites.Get(s.Id)!.Pages[0].Blocks[0].Text.Should().StartWith("Skriv her");
        _editor.Undo(s.Id).Should().BeFalse();
    }

    [Test]
    public void Deleted_page_comes_back_with_undo()
    {
        var s = _sites.Create("Test", null, Package.Selv, null, null);
        _editor.RemovePage(s.Id, "om-os");
        _sites.Get(s.Id)!.Pages.Should().HaveCount(2);
        _editor.Undo(s.Id);
        _sites.Get(s.Id)!.Pages.Select(p => p.Slug).Should().Contain("om-os");
    }

    [Test]
    public void Front_page_cannot_be_deleted_hidden_or_moved()
    {
        var s = _sites.Create("Test", null, Package.Selv, null, null);
        FluentActions.Invoking(() => _editor.RemovePage(s.Id, "forside")).Should().Throw<SiteException>();
        FluentActions.Invoking(() => _editor.ShowInMenu(s.Id, "forside", false)).Should().Throw<SiteException>();
        _editor.MovePage(s.Id, "om-os", -1);
        _sites.Get(s.Id)!.Pages[0].Slug.Should().Be("forside");
    }

    [Test]
    public void New_page_gets_a_plain_unique_address()
    {
        var s = _sites.Create("Test", null, Package.Selv, null, null);
        _editor.AddPage(s.Id, "Åbningstider & priser").Slug.Should().Be("aabningstider-priser");
        _editor.AddPage(s.Id, "Åbningstider & priser").Slug.Should().Be("aabningstider-priser-2");
        _editor.AddPage(s.Id, "Ret").Slug.Should().Be("ret-2");
    }

    [Test]
    public void Owner_code_opens_only_its_own_site_and_not_on_full_help()
    {
        var access = new Access("martin-kode", TimeProvider.System);
        var a = _sites.Create("Anna", null, Package.Selv, "kode-aaa", null);
        var b = _sites.Create("Bente", null, Package.Selv, "kode-bbb", null);
        access.Login("kode-aaa", a, "1").Should().Be(LoginResult.Owner);
        access.Login("kode-aaa", b, "1").Should().Be(LoginResult.Wrong);
        access.Login("martin-kode", b, "1").Should().Be(LoginResult.Martin);
        Access.IsOwner(b, Access.OwnerToken(a)).Should().BeFalse();

        var full = _sites.SetUp(a.Id, "Anna", "", "", "", "", [], Package.Fuld, null, "", null);
        access.Login("kode-aaa", full, "2").Should().Be(LoginResult.Wrong);
        Access.IsOwner(full, Access.OwnerToken(a)).Should().BeFalse();
    }

    [Test]
    public void Ten_wrong_codes_lock_the_address()
    {
        var access = new Access("martin-kode", TimeProvider.System);
        for (var i = 0; i < 10; i++) access.Login("forkert", null, "9.9.9.9");
        access.Login("martin-kode", null, "9.9.9.9").Should().Be(LoginResult.Locked);
        access.Login("martin-kode", null, "1.1.1.1").Should().Be(LoginResult.Martin);
    }

    [Test]
    public void Price_and_notes_are_Martins_own()
    {
        var s = _sites.Create("Tester", null, Package.Sammen, null, null);
        var after = _sites.SetUp(s.Id, "Tester", "", "#123456", "", "", [], Package.Sammen, 0, "Tester: gratis opsætning", null);
        after.Price.Should().Be(0);
        after.Note.Should().Be("Tester: gratis opsætning");
        after.Accent.Should().Be("#123456");
    }

    [Test]
    public void Text_is_encoded_and_gets_headings_lists_and_bold()
    {
        var html = SiteHtml.Text("## Tider\n- Man **10-17**\n- Tir\n\nHej <script>x</script>");
        html.Should().Be("<h2>Tider</h2><ul><li>Man <b>10-17</b></li><li>Tir</li></ul><p>Hej &lt;script&gt;x&lt;/script&gt;</p>");
    }

    [Test]
    public void Preview_and_own_domain_use_different_links()
    {
        var s = _sites.Create("Test", null, Package.Selv, null, null);
        SiteHtml.Page(s, s.Pages[1], "/s/test").Should().Contain("href=\"/s/test/kontakt\"");
        SiteHtml.Page(s, s.Pages[1], "").Should().Contain("href=\"/kontakt\"").And.Contain("aria-current=\"page\">Om os");
    }

    [Test]
    public async Task Pictures_are_shrunk_and_unused_ones_freed()
    {
        var s = _sites.Create("Test", null, Package.Selv, null, null);
        using var img = new SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgb24>(3000, 2000);
        using var ms = new MemoryStream();
        await SixLabors.ImageSharp.ImageExtensions.SaveAsPngAsync(img, ms);
        ms.Position = 0;

        var block = await _editor.PictureAsync(s.Id, "forside", null, ms, "Skoven", CancellationToken.None);
        using (var stored = SixLabors.ImageSharp.Image.Load(_folders.Open(s.Id, block.Picture!)!))
            stored.Width.Should().Be(1600);

        _editor.RemoveBlock(s.Id, block.Id);
        _folders.Open(s.Id, block.Picture!).Should().NotBeNull("Fortryd can still bring it back");
        _editor.Undo(s.Id).Should().BeTrue();
        _sites.Get(s.Id)!.Pictures().Should().Contain(block.Picture!);
    }
}

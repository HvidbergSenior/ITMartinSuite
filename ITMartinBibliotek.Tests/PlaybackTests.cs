using FluentAssertions;
using ITMartinBibliotek.Application;
using ITMartinBibliotek.Domain;
using Moq;

namespace ITMartinBibliotek.Tests;

public class PlaybackTests
{
    private const string Disc1 = "11111111111111111111111111111111";
    private const string Disc2 = "22222222222222222222222222222222";
    private const string Copy = "33333333333333333333333333333333";

    private static PlayerTrack T(string id, string name, int seconds, int n) => new(id, name, "Bowie", seconds, 1, n);

    private static (Playback playback, Mock<IMediaRepository> repo) Make()
    {
        var jf = new Mock<IJellyfin>();
        jf.Setup(j => j.AlbumAsync(Disc1)).ReturnsAsync(new PlayerAlbum(Disc1, "Beeb", "Bowie", 2000, "c", [T("a", "Changes", 200, 1), T("b", "Kooks", 170, 2)]));
        jf.Setup(j => j.AlbumAsync(Disc2)).ReturnsAsync(new PlayerAlbum(Disc2, "Beeb", "Bowie", 2000, "c", [T("c", "Heroes", 300, 1)]));
        jf.Setup(j => j.AlbumAsync(Copy)).ReturnsAsync(new PlayerAlbum(Copy, "Beeb", "Bowie", 2000, "c", [T("x", "Changes", 201, 1), T("y", "Kooks!", 170, 2)]));
        var repo = new Mock<IMediaRepository>();
        return (new Playback(repo.Object, jf.Object), repo);
    }

    [Test]
    public async Task A_joined_album_plays_both_cds_in_order()
    {
        var (p, _) = Make();
        var album = await p.AlbumAsync(Disc1 + "_" + Disc2);

        album!.Id.Should().Be(Disc1 + "_" + Disc2);
        album.Tracks.Select(t => (t.Id, t.Disc)).Should().Equal(("a", 1), ("b", 1), ("c", 2));
    }

    [Test]
    public async Task A_second_copy_of_the_same_album_does_not_play_every_song_twice()
    {
        var (p, _) = Make();
        var album = await p.AlbumAsync(Disc1 + "_" + Copy);

        album!.Tracks.Select(t => t.Id).Should().Equal("a", "b");
    }

    [TestCase(Disc1, true)]
    [TestCase(Disc1 + "_" + Disc2, true)]
    [TestCase("", false)]
    [TestCase("../etc", false)]
    [TestCase(Disc1 + "_x", false)]
    public void Only_jellyfin_ids_are_accepted(string playId, bool ok) => Playback.IsPlayId(playId).Should().Be(ok);

    [Test]
    public async Task A_customer_may_play_a_joined_album_only_when_every_part_is_theirs()
    {
        var (p, repo) = Make();
        var jytte = new Viewer("jytte", "Jytte");
        repo.Setup(r => r.OwnsAsync("jytte", Disc1)).ReturnsAsync(true);
        repo.Setup(r => r.OwnsAsync("jytte", Disc2)).ReturnsAsync(false);

        (await p.MayPlayAsync(jytte, Disc1)).Should().BeTrue();
        (await p.MayPlayAsync(jytte, Disc1 + "_" + Disc2)).Should().BeFalse();
        (await p.MayPlayAsync(Viewer.Admin, Disc1 + "_" + Disc2)).Should().BeTrue();
    }

    [Test]
    public async Task The_same_song_twice_on_one_cd_is_kept()
    {
        var jf = new Mock<IJellyfin>();
        jf.Setup(j => j.AlbumAsync(Disc1)).ReturnsAsync(new PlayerAlbum(Disc1, "Beeb (Disc 1)", "Bowie", 2000, "c",
            [T("a", "Heat", 200, 1), T("b", "Heat", 200, 2)]));
        jf.Setup(j => j.AlbumAsync(Disc2)).ReturnsAsync(new PlayerAlbum(Disc2, "Beeb (Disc 2)", "Bowie", 2000, "c",
            [T("c", "Heat", 200, 1), T("d", "Kooks", 170, 2)]));
        var p = new Playback(new Mock<IMediaRepository>().Object, jf.Object);

        var album = await p.AlbumAsync(Disc1 + "_" + Disc2);

        album!.Name.Should().Be("Beeb");
        album.Tracks.Select(t => t.Id).Should().Equal("a", "b", "c", "d");
    }
}

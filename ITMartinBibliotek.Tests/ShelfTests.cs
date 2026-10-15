using FluentAssertions;
using ITMartinBibliotek.Domain;

namespace ITMartinBibliotek.Tests;

public class ShelfTests
{
    private static MediaItem Music(string artist, string title, int? year = null, string jf = "", string cover = "") => new()
    {
        Kind = MediaKind.Musik, Artist = artist, Title = title, Year = year,
        JellyfinId = jf == "" ? Guid.NewGuid().ToString("N") : jf, CoverUrl = cover,
    };

    private static MediaItem Film(string title, string collection = "") => new() { Kind = MediaKind.Film, Title = title, Collection = collection };

    [TestCase("Echoes: The Best of Pink Floyd (Disc 1)", "Echoes: The Best of Pink Floyd", 1)]
    [TestCase("Remixes 81-04 [Disc 2]", "Remixes 81-04", 2)]
    [TestCase("Gilmore Girls: The Ultimate Soundtrack Vol. 1 [CD 1]", "Gilmore Girls: The Ultimate Soundtrack Vol. 1", 1)]
    [TestCase("The Greatest Hits CD2", "The Greatest Hits", 2)]
    [TestCase("Live - Disk 1 af 2", "Live", 1)]
    [TestCase("Up To Now", "Up To Now", 0)]
    [TestCase("CD 1", "CD 1", 0)]
    [TestCase("Discovery", "Discovery", 0)]
    public void SplitDisc_finds_the_disc_marker_at_the_end_only(string title, string expected, int disc) =>
        Shelf.SplitDisc(title).Should().Be((expected, disc));

    [Test]
    public void A_two_cd_set_and_a_same_named_double_become_one_album_in_disc_order()
    {
        var d2 = Music("David Bowie", "Bowie At The Beeb (Disc 2)", 2000, cover: "c2");
        var d1 = Music("David Bowie", "Bowie At The Beeb (Disc 1)", 2000);
        var a = Music("John Lennon", "Working Class Hero", 2005);
        var b = Music("john lennon", "Working Class Hero", 2005);

        var albums = Shelf.Albums([d2, d1, a, b]);

        albums.Should().HaveCount(2);
        var beeb = albums.Single(x => x.Title == "Bowie At The Beeb");
        beeb.Parts.Should().Equal(d1, d2);
        beeb.CoverUrl.Should().Be("c2");
        beeb.PlayId.Should().Be(d1.JellyfinId + "_" + d2.JellyfinId);
        albums.Single(x => x.Title == "Working Class Hero").Parts.Should().HaveCount(2);
    }

    [Test]
    public void Artists_are_sorted_like_a_danish_record_shelf()
    {
        var names = new[] { "Østergaard", "The Beatles", "ABBA", "Aqua", "Ærø Band", "Ålborg Kor", "2Pac", "Bob Dylan", "Élodie" };
        var artists = Shelf.Artists(Shelf.Albums(names.Select(n => Music(n, "Album"))));

        artists.Select(a => a.Name).Should().Equal("ABBA", "Aqua", "The Beatles", "Bob Dylan", "Élodie", "Ærø Band", "Østergaard", "Ålborg Kor", "2Pac");
        artists.Select(a => a.Letter).Should().Equal("A", "A", "B", "B", "E", "Æ", "Ø", "Å", "#");
    }

    [TestCase("Bowie At The Beeb!", "bowie at the beeb")]
    [TestCase("Élodie", "elodie")]
    [TestCase("Ålborg", "ålborg")]
    [TestCase("Rabalderstræde Forever", "rabalderstræde forever")]
    [TestCase("AC/DC", "ac dc")]
    public void Fold_drops_accents_and_punctuation_but_keeps_danish_letters(string input, string expected) =>
        Shelf.Fold(input).Should().Be(expected);

    [Test]
    public void Search_looks_everywhere_not_only_in_one_shelf()
    {
        var items = new List<MediaItem>
        {
            Music("David Bowie", "Heathen"), Music("David Bowie", "Low"), Music("Bowie Tribute Band", "Live"),
            Music("Kim Larsen", "Midt om natten"), Film("Labyrinth (with David Bowie)"), Film("X2", "X-Men"),
        };

        var r = Shelf.Search(items, "bowie");

        r.Artists.Select(a => a.Name).Should().Equal("Bowie Tribute Band", "David Bowie");
        r.Albums.Should().HaveCount(3);
        r.Titles.Select(t => t.Title).Should().Equal("Labyrinth (with David Bowie)");
        Shelf.Search(items, "x-men").Titles.Select(t => t.Title).Should().Equal("X2");
        Shelf.Search(items, "midt natten").Albums.Single().Title.Should().Be("Midt om natten");
        Shelf.Search(items, "  ").IsEmpty.Should().BeTrue();
    }

    [Test]
    public void Search_ignores_accents_and_ranks_names_that_start_with_the_query_first()
    {
        var items = new List<MediaItem> { Music("Larsen Kim", "A"), Music("Kim Larsen", "B"), Music("Søs Fenger", "C") };

        Shelf.Search(items, "kim").Artists.Select(a => a.Name).Should().Equal("Kim Larsen", "Larsen Kim");
        Shelf.Search(items, "sos").Artists.Should().BeEmpty("ø is its own letter, not an o with a mark");
        Shelf.Search(items, "søs").Artists.Single().Name.Should().Be("Søs Fenger");
    }

    [Test]
    public void The_and_apostrophes_do_not_split_an_artist_in_two()
    {
        var artists = Shelf.Artists(Shelf.Albums([
            Music("The B‐52s", "Cosmic Thing"), Music("The B‐52’s", "Wild Planet"),
            Music("Beastie Boys", "Ill Communication"), Music("The Beastie Boys", "Licensed to Ill"), Music("Beastie Boys", "Hello Nasty")]));

        artists.Select(a => (a.Name, a.Albums.Count)).Should().Equal(("The B‐52s", 2), ("Beastie Boys", 3));
    }
}

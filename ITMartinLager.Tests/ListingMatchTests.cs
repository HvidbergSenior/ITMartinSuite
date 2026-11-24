using FluentAssertions;
using ITMartinLager.Domain;

namespace ITMartinLager.Tests;

// The rules from the first Stikprøve (2026-11-24) - every case here was marked Forkert by Martin.
public class ListingMatchTests
{
    private static Item It(string kind, string title, string artist = "") => new() { Kind = kind, Title = title, Artist = artist };

    [TestCase("Criminal Minds Sæson 1, 2, 5, 8, 9, 10, 11, 12")]
    [TestCase("Criminal Minds sæson 1-7")]
    [TestCase("Criminal Minds sæson 3, 4, 5, 6 og 8")]
    [TestCase("Criminal Minds , sæson 5")]
    [TestCase("Criminal Minds")]
    public void Another_or_several_seasons_is_wrong(string listing) =>
        ListingMatch.Wrong(It(Kinds.Dvd, "Criminal Minds – The Second Season"), listing).Should().BeTrue();

    [TestCase("Criminal Minds sæson 2")]
    [TestCase("Criminal Minds - The Complete Second Season")]
    [TestCase("Criminal Minds season two")]
    public void The_same_season_is_right(string listing) =>
        ListingMatch.Wrong(It(Kinds.Dvd, "Criminal Minds – The Second Season"), listing).Should().BeFalse();

    [Test]
    public void Seasons_reads_ranges_lists_and_words()
    {
        ListingMatch.Seasons("sæson 1-3").Should().BeEquivalentTo([1, 2, 3]);
        ListingMatch.Seasons("sæson 3, 4 og 8").Should().BeEquivalentTo([3, 4, 8]);
        ListingMatch.Seasons("The Second Season").Should().BeEquivalentTo([2]);
        ListingMatch.Seasons("In Treatment – Season 1: Weeks 6-9").Should().BeEquivalentTo([1]);
    }

    [Test]
    public void A_cd_with_only_the_artist_as_title_has_no_album() =>
        ListingMatch.NoAlbum(It(Kinds.Cd, "Rufus Wainwright", "Rufus Wainwright")).Should().BeTrue();

    [TestCase("2CDBOKS : Jonas Brothers - A Little Bit Longer/ Jonas Brothers - 2 cd’er 25kr i alt")]
    [TestCase("Jonas Brothers - A little bit longer  pop/rock 2 cd’er 25kr i alt")]
    public void Several_things_at_once_is_wrong(string listing) =>
        ListingMatch.Wrong(It(Kinds.Cd, "A Little Bit Longer", "Jonas Brothers"), listing).Should().BeTrue();

    [TestCase("SANNE SALOMONSEN. 3 x Cd.")]
    [TestCase("SANNE SALOMONSEN: 4 titler: stk. 20 kr. - Alle 50 ")]
    [TestCase("Sanne Salomonsen 10 stk")]
    public void A_count_of_things_is_a_bundle(string listing) =>
        ListingMatch.Bundle(listing, It(Kinds.Cd, "In a New York Minute", "Sanne Salomonsen")).Should().BeTrue();

    [Test]
    public void A_double_album_is_not_a_bundle() =>
        ListingMatch.Bundle("Sanne Salomonsen - Unplugged 2 x CD", It(Kinds.Cd, "Unplugged", "Sanne Salomonsen")).Should().BeFalse();

    [Test]
    public void An_album_named_after_part_of_the_artist_has_no_album() =>
        ListingMatch.NoAlbum(It(Kinds.Cd, "Sanne", "Sanne Salomonsen")).Should().BeTrue();

    [Test]
    public void A_real_album_title_is_an_album() =>
        ListingMatch.NoAlbum(It(Kinds.Cd, "Evita", "Sanne Salomonsen")).Should().BeFalse();

    [TestCase("Frokoster - Sund & slank året rundt, Inge Kauffeldt")]
    [TestCase("Deluxe - Sund & slank året rundt")]
    public void Another_book_in_the_series_is_wrong(string listing) =>
        ListingMatch.Wrong(It(Kinds.Bog, "Desserter – Sund & slank året rundt"), listing).Should().BeTrue();

    [Test]
    public void The_same_book_is_right() =>
        ListingMatch.Wrong(It(Kinds.Bog, "Desserter – Sund & slank året rundt"), "Desserter, Sund og slank året rundt").Should().BeFalse();

    [Test]
    public void Sealed_is_a_special_version() =>
        FixedPrice.SpecialVersion("Rend Mig I Traditionerne (UÅBNET, stadig i folie)", It(Kinds.Dvd, "Rend mig i traditionerne")).Should().BeTrue();
}

using FluentAssertions;
using ITMartinLager.Domain;

namespace ITMartinLager.Tests;

// Stikprøve round 2 (user 2026-11-24): every listing here was marked Forkert - none may count for the price.
public class StikproeveRound2Tests
{
    private static bool Counts(Item i, string listing) =>
        !ListingMatch.NoAlbum(i) && !FixedPrice.SpecialVersion(listing, i) && !FixedPrice.OtherIssue(i, listing) && !ListingMatch.Wrong(i, listing);

    private static Item Blad(string title, string series, string number, int year) =>
        new() { Kind = Kinds.Tegneserie, Title = title, Series = series, Number = number, Year = year };

    [TestCase("Kong Kylie 1951 Nr. 23")]
    [TestCase("Kong Kylie 1950 Nr. 23")]
    public void Kong_Kylie_other_year(string l) => Counts(Blad("Da Svend Asmussen ville være tandlæge", "Kong Kylie", "23", 1949), l).Should().BeFalse();

    [TestCase("Sølvpil 1978 Nr. 47 Sølvpil - Det Friske Indianer-Blad for Drenge og Piger")]
    [TestCase("Sølvpil 1976 Nr. 47 Sølvpil - Det Friske Indianer-Blad for Drenge og Piger")]
    public void Soelvpil_other_year(string l) => Counts(Blad("Sølvpil", "Sølvpil", "47", 1977), l).Should().BeFalse();

    [TestCase("SØLVPIL 1978, Tegneserie")]
    [TestCase("Sølvpil 1978")]
    public void Soelvpil_listing_without_a_number(string l) => Counts(Blad("Sølvpil", "Sølvpil", "48", 1978), l).Should().BeFalse();

    [Test]
    public void Two_cds() => Counts(new Item { Kind = Kinds.Cd, Title = "From the Cradle", Artist = "Eric Clapton" },
        "CD: Eric Clapton - \"From The Cradle\" og \"Reptile\" - 2 cd'er").Should().BeFalse();

    [TestCase("Frokoster - Sund & slank året rundt, Inge Kauffeldt & Helle Kleist, emne: mad")]
    [TestCase("Deluxe - Sund & slank året rundt")]
    public void Other_book_in_series(string l) => Counts(new Item { Kind = Kinds.Bog, Title = "Middage – Sund & slank året rundt" }, l).Should().BeFalse();

    [TestCase("[ny i folie] Spas - Mick Øgendahl")]
    [TestCase("Micktrix, Mick Øgendahl.")]
    [TestCase("JULEFROKOSTEN. Mick Øgendahl. BYD.")]
    public void Dvd_titled_only_by_the_artist(string l) => Counts(new Item { Kind = Kinds.Dvd, Title = "Mick Øgendahl", Artist = "Mick Øgendahl" }, l).Should().BeFalse();

    [TestCase("Sanne Salomonsen – Evita – CD")]
    [TestCase("SANNE SALOMONSEN. 3 x Cd.")]
    public void Sanne(string l) => Counts(new Item { Kind = Kinds.Cd, Title = "Sanne", Artist = "Sanne Salomonsen" }, l).Should().BeFalse();
}

using FluentAssertions;
using ITMartinHjem.Domain;

namespace ITMartinHjem.Tests;

// "Det leder folk efter" on bogshoppen.dk (2026-11-19): ticked groups counted, own words shown, itmartin.dk parts and
// contact details left out.
public class BogshoppenWishesTests
{
    [Test]
    public void Groups_are_counted_and_own_words_kept()
    {
        var s = BogshoppenWishes.Summarize([
            "Bøger, Musik på cd | Isaac Asimov, og generelt bare sci-fi",
            "Film på dvd og Blu-ray",
            "Superhelte fra DC og Marvel, på engelsk, Bøger",
            "Anders And-blade, Konsolspil, Andet – skriv det i kommentaren | underlige bøger",
            "Jeg leder efter spillet: Sly Cooper: thieves in time til ps3",
        ]);

        s.People.Should().Be(5);
        s.Groups.First().Should().Be(new BogshoppenWishes.Group("Bøger", 2));
        s.Groups.Should().Contain(new BogshoppenWishes.Group("Superhelte fra DC og Marvel, på engelsk", 1));
        s.Groups.Should().Contain(new BogshoppenWishes.Group("Anders And", 1));
        s.Groups.Should().Contain(new BogshoppenWishes.Group("Konsolspil", 1));
        s.Wishes.Should().Equal("Isaac Asimov, og generelt bare sci-fi", "underlige bøger",
            "Jeg leder efter spillet: Sly Cooper: thieves in time til ps3");
    }

    [Test]
    public void ItMartin_parts_are_left_out()
    {
        var s = BogshoppenWishes.Summarize([
            "Gratis apps (ElPriser, Tilbud, Budget …), Bogshoppens nye måder at sælge på ; itmartin.dk ; ElPriser",
            "Musik på cd ; itmartin.dk",
            "Budget app, bedre end Spiiri.",
        ]);

        s.People.Should().Be(1);
        s.Groups.Should().Equal(new BogshoppenWishes.Group("Musik på cd", 1));
        s.Wishes.Should().BeEmpty();
    }

    [Test]
    public void Contact_details_are_removed_and_long_words_cut()
    {
        BogshoppenWishes.Clean("Ring på 31 19 47 30 eller skriv til a@b.dk om Tintin").Should().Be("Ring på eller skriv til om Tintin");
        BogshoppenWishes.Clean(new string('x', 10) + " " + string.Join(" ", Enumerable.Repeat("ord", 60))).Length.Should().BeLessThanOrEqualTo(BogshoppenWishes.MaxWish + 2);
        BogshoppenWishes.Clean(" ok ").Should().Be("");
    }
}

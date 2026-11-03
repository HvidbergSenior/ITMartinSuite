using FluentAssertions;
using ITMartinLager.Domain;
using NUnit.Framework;

namespace ITMartinLager.Tests;

// A-Å on bogshoppen.dk (user 2026-11-03: "but it is not alphateciacl?").
public class NameOrderTests
{
    private static int _id;
    private static Item I(string title, string artist = "", string number = "", int? year = null) =>
        new() { Id = ++_id, Title = title, Artist = artist, Number = number, Year = year };

    private static List<string> Sorted(params Item[] items) =>
        items.Order(NameOrder.Instance).Select(i => (i.Artist != "" ? i.Artist + " · " : "") + i.Title + (i.Number != "" ? " nr. " + i.Number : "")).ToList();

    [Test]
    public void The_card_s_first_word_counts_artist_before_title() =>
        Sorted(I("1+1+1+1", "Mark Solborg 4"), I("9", "Damien Rice"), I("Acoustic Soul", "India.Arie"))
            .Should().Equal("Damien Rice · 9", "India.Arie · Acoustic Soul", "Mark Solborg 4 · 1+1+1+1");

    [Test]
    public void Danish_letters_come_after_z_and_capitals_do_not_jump_ahead() =>
        Sorted(I("Åen"), I("Ønsket"), I("Æblet"), I("zorro"), I("Asterix"), I("barnaby"))
            .Should().Equal("Asterix", "barnaby", "zorro", "Æblet", "Ønsket", "Åen");

    [Test]
    public void The_same_title_goes_by_year_then_number_as_a_number() =>
        Sorted(I("Jonah Hex", number: "10", year: 1980), I("Jonah Hex", number: "9", year: 1980), I("Jonah Hex", number: "2", year: 1985))
            .Should().Equal("Jonah Hex nr. 9", "Jonah Hex nr. 10", "Jonah Hex nr. 2");
}

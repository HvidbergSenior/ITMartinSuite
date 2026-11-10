using FluentAssertions;
using ITMartinLager.Domain;

namespace ITMartinLager.Tests;

// Ting/Strik descriptions (user 2026-11-10: "why no description?" + "i want to have ai look on the things").
public class ThingTextTests
{
    private static readonly DateTime Now = new(2026, 11, 10, 20, 0, 0, DateTimeKind.Utc);

    private static Item Thing(string kind, string title, string interest = "") => new()
    {
        Id = 1, Kind = kind, Title = title, Interest = interest, ItemPhoto = "photos/items/1.jpg", Status = Statuses.Lager,
    };

    [Test]
    public void A_one_word_title_gets_the_fuller_one_but_a_person_s_own_title_and_text_are_kept()
    {
        var stol = Thing(Kinds.Ting, "Stol");
        ThingText.Apply(stol, "Safaristol i læder og bøg", "En safaristol er et sammenklappeligt møbel ...", 1500, 4000, Now);
        stol.Title.Should().Be("Safaristol i læder og bøg");
        stol.Interest.Should().StartWith("En safaristol");
        stol.PriceHint.Should().Be(2750m, "the middle of the range, in tens");
        stol.DescribedAt.Should().Be(Now);
        ThingText.Wants(stol).Should().BeFalse("it was looked at");

        var vase = Thing(Kinds.Ting, "Böhmisk rubinglas-vase med hjort", "Min egen tekst");
        ThingText.Apply(vase, "Vase", "AI-tekst", null, null, Now);
        vase.Title.Should().Be("Böhmisk rubinglas-vase med hjort");
        vase.Interest.Should().Be("Min egen tekst");
        vase.PriceHint.Should().BeNull();
    }

    [Test]
    public void Strik_gets_no_price_suggestion_and_an_empty_answer_is_still_marked_looked_at()
    {
        var sweater = Thing(Kinds.Strik, "Sweater");
        ThingText.Apply(sweater, "Håndstrikket sweater med snoninger", "Strikket i uld ...", 200, 400, Now);
        sweater.PriceHint.Should().BeNull("the maker prices Strik");

        var kabinet = Thing(Kinds.Ting, "Kabinet");
        ThingText.Apply(kabinet, "", "", null, null, Now);
        kabinet.Title.Should().Be("Kabinet");
        kabinet.DescribedAt.Should().Be(Now);
    }

    [Test]
    public void Only_Ting_and_Strik_with_a_picture_for_sale_and_no_text_are_wanted()
    {
        ThingText.Wants(Thing(Kinds.Ting, "Stol")).Should().BeTrue();
        ThingText.Wants(Thing(Kinds.Ting, "Stol", "har tekst")).Should().BeFalse();
        ThingText.Wants(Thing(Kinds.Bog, "Mort")).Should().BeFalse();
        var noPhoto = Thing(Kinds.Ting, "Stol"); noPhoto.ItemPhoto = "";
        ThingText.Wants(noPhoto).Should().BeFalse();
        var sold = Thing(Kinds.Ting, "Stol"); sold.Status = Statuses.Solgt;
        ThingText.Wants(sold).Should().BeFalse();
    }
}

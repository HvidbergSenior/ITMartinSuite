using FluentAssertions;
using ITMartinLager.Domain;

namespace ITMartinLager.Tests;

// New or used (user 2026-10-20: "there should be a used and new field").
public class UsageTests
{
    private static readonly DateTime Now = new(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);
    private static Offer O(string name, decimal kr) => new(name, kr, "https://x/" + kr);

    [Test]
    public void Anything_unknown_is_used()
    {
        Usages.Normalise("ny").Should().Be(Usages.New);
        Usages.Normalise(null).Should().Be(Usages.Used);
        Usages.Normalise("forseglet").Should().Be(Usages.Used);
        Item.From(new Found(Kinds.Bog, "Mort", "", "", "", null, "", "God", 1, 0.9, "", Usage: "Ny"), null, null).Usage.Should().Be(Usages.New);
    }

    [Test]
    public void A_new_copy_is_priced_under_the_new_shops_and_a_used_one_under_the_used_market()
    {
        var answers = new List<SourceAnswer>
        {
            new("DBA", false, ListingParse.Summarise([O("Mort", 50), O("Mort", 60)], "u")),
            new("Saxo", true, ListingParse.Summarise([O("Mort", 200)], "u")),
        };
        var used = new Item { Kind = Kinds.Bog, Title = "Mort" };
        var fresh = new Item { Kind = Kinds.Bog, Title = "Mort", Usage = Usages.New };
        HuntResult.From(1, answers, Now).ApplyTo(used, Now);
        HuntResult.From(2, answers, Now).ApplyTo(fresh, Now);
        used.PriceHint.Should().BeLessThan(60);
        fresh.PriceHint.Should().Be(180);   // 10 % under the new shop
    }

    [Test]
    public void A_new_and_a_used_copy_of_the_same_book_are_two_rows()
    {
        var used = new Item { Kind = Kinds.Bog, Title = "Mort", Condition = "God" };
        used.SameAs(new Found(Kinds.Bog, "Mort", "", "", "", null, "", "God", 1, 1, "", Usage: Usages.New)).Should().BeFalse();
        used.SameAs(new Found(Kinds.Bog, "Mort", "", "", "", null, "", "God", 1, 1, "")).Should().BeTrue();
    }
}

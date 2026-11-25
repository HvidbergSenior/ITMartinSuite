using FluentAssertions;
using ITMartinLager.Domain;
using NUnit.Framework;

namespace ITMartinLager.Tests;

public class IssueHintTests
{
    private static Item Blad(string number) => new() { Kind = Kinds.AndersAnd, Title = "Anders And & Co.", Number = number };
    private static PriceFind F(string name, int? cover = null) => new() { Name = name, Cover = cover };

    [Test]
    public void Agree_when_every_listing_names_our_number()
    {
        var h = IssueHints.For(Blad("12"), [F("Anders And & Co. nr. 12 1998"), F("Anders And nr 12")]);
        h.Kind.Should().Be(IssueHintKind.Agree);
    }

    [Test]
    public void Other_when_the_listings_agree_with_each_other_but_not_with_us()
    {
        var h = IssueHints.For(Blad("12"), [F("Anders And nr. 15"), F("Anders And Nr.15 1998")]);
        h.Kind.Should().Be(IssueHintKind.Other);
        h.SuggestedNumber.Should().Be("15");
    }

    [Test]
    public void Other_when_we_have_no_number_at_all()
    {
        var h = IssueHints.For(Blad(""), [F("Anders And nr. 15")]);
        h.Kind.Should().Be(IssueHintKind.Other);
        h.SuggestedNumber.Should().Be("15");
    }

    [Test]
    public void Mixed_when_the_listings_disagree()
    {
        var h = IssueHints.For(Blad("12"), [F("Anders And nr. 12"), F("Anders And nr. 15"), F("Anders And nr. 21")]);
        h.Kind.Should().Be(IssueHintKind.Mixed);
        h.SuggestedNumber.Should().BeNull();
    }

    [Test]
    public void A_clear_majority_for_our_number_is_still_agree()
    {
        var h = IssueHints.For(Blad("12"), [F("nr. 12"), F("nr. 12"), F("nr. 12"), F("nr. 15")]);
        h.Kind.Should().Be(IssueHintKind.Agree);
    }

    [Test]
    public void A_listing_with_another_editions_cover_is_left_out_and_a_same_cover_counts_double()
    {
        IssueHints.For(Blad("12"), [F("nr. 12", cover: 40), F("nr. 15", cover: 3)]).Kind.Should().Be(IssueHintKind.Agree);
        IssueHints.For(Blad("12"), [F("nr. 12", cover: 40), F("nr. 15"), F("nr. 15")]).Kind.Should().Be(IssueHintKind.Mixed);
    }

    [Test]
    public void None_when_no_listing_names_a_number()
    {
        IssueHints.For(Blad("12"), [F("Anders And Ekstra"), F("Anders And 1998")]).Kind.Should().Be(IssueHintKind.None);
    }

    [Test]
    public void Order_puts_mixed_first_then_other()
    {
        new IssueHint(IssueHintKind.Mixed, "", null).Order.Should().BeLessThan(new IssueHint(IssueHintKind.Other, "", null).Order);
        new IssueHint(IssueHintKind.Other, "", null).Order.Should().BeLessThan(new IssueHint(IssueHintKind.Agree, "", null).Order);
    }
}

using FluentAssertions;
using ITMartinVarer.Contracts;
using ITMartinVurdering.Application;
using ITMartinVurdering.Domain;
using Moq;

namespace ITMartinVurdering.Tests;

public class ReviewTests
{
    [TestCase("120", 120)]
    [TestCase(" 120 kr ", 120)]
    [TestCase("120,-", 120)]
    [TestCase("1.200", 1200)]
    [TestCase("49,50", 49)]
    public void Typed_prices_become_whole_kroner(string typed, int kr) => PriceRules.Parse(typed).Should().Be(kr);

    [TestCase("")]
    [TestCase("abc")]
    [TestCase("0")]
    [TestCase("100001")]
    public void Not_a_price_is_refused(string typed) => PriceRules.Parse(typed).Should().BeNull();

    [Test]
    public async Task Ok_saves_the_suggestion_and_own_price_saves_what_was_typed()
    {
        var lager = new Mock<ILagerReview>();
        var uc = new ReviewUseCases(lager.Object);
        (await uc.OkAsync(7, 85)).Ok.Should().BeTrue();
        lager.Verify(l => l.SetPriceAsync(7, 85, It.IsAny<CancellationToken>()));
        (await uc.OwnAsync(7, "60 kr")).Message.Should().Be("Gemt: 60 kr.");
        lager.Verify(l => l.SetPriceAsync(7, 60, It.IsAny<CancellationToken>()));
    }

    [Test]
    public async Task Ok_without_a_suggestion_asks_for_a_price_and_saves_nothing()
    {
        var lager = new Mock<ILagerReview>();
        (await new ReviewUseCases(lager.Object).OkAsync(7, null)).Ok.Should().BeFalse();
        lager.Verify(l => l.SetPriceAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Lagers_reason_is_passed_on()
    {
        var lager = new Mock<ILagerReview>();
        lager.Setup(l => l.SetPriceAsync(7, 85, It.IsAny<CancellationToken>())).ReturnsAsync("Varen er solgt.");
        (await new ReviewUseCases(lager.Object).OkAsync(7, 85)).Message.Should().Be("Varen er solgt.");
    }

    [Test]
    public async Task An_unknown_pile_falls_back_to_Skal_vurderes()
    {
        var lager = new Mock<ILagerReview>();
        await new ReviewUseCases(lager.Object).NextAsync("hack", []);
        lager.Verify(l => l.QueueAsync(VurderingPiles.Assess, 1, It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()));
    }
}

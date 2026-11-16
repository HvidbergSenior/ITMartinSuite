using FluentAssertions;
using ITMartinSalg.Domain;
using ITMartinVarer.Contracts;
using NUnit.Framework;

namespace ITMartinSalg.Tests;

// Mængderabat (2026-11-16): 5+ 10 %, 10+ 20 %, 20+ 30 %, 50+ 40 %, 100+ 50 %; the 3/5 kr bins count but keep their price.
public class QuantityRabatTests
{
    private static (PricedBasket, List<BasketLine>) Basket(int books, int vhs = 0)
    {
        var lines = Enumerable.Range(1, books).Select(n => new BasketLine(n, Kinds.Bog, 25))
            .Concat(Enumerable.Range(1000, vhs).Select(n => new BasketLine(n, Kinds.Vhs, 5))).ToList();
        return (new PricedBasket(lines.Select(l => new PricedLine(l.ItemId, l.Kr, l.Kr, "")).ToList()), lines);
    }

    [TestCase(4, 0)] [TestCase(5, 10)] [TestCase(10, 20)] [TestCase(20, 30)] [TestCase(50, 40)] [TestCase(100, 50)] [TestCase(250, 50)]
    public void The_step_follows_the_count(int count, int percent) => QuantityRabat.PercentFor(count).Should().Be(percent);

    [Test]
    public void Ten_books_cost_20_percent_less()
    {
        var (priced, lines) = Basket(10);
        var r = QuantityRabat.Apply(priced, lines);
        r.Total.Should().Be(200);
        r.Saved.Should().Be(50);
        r.Lines[0].Offer.Should().Be("Mængderabat 20 %");
    }

    [Test]
    public void The_bins_count_but_keep_their_price()
    {
        var (priced, lines) = Basket(3, vhs: 2);   // 5 things -> 10 %, only on the books
        var r = QuantityRabat.Apply(priced, lines);
        r.Lines.Where(l => l.ItemId >= 1000).Should().OnlyContain(l => l.Kr == 5);
        r.Lines.Where(l => l.ItemId < 1000).Should().OnlyContain(l => l.Kr == 22);
    }

    [Test]
    public void The_next_step_is_told() 
    {
        QuantityRabat.Next(6).Should().Be((4, 20));
        QuantityRabat.Next(100).Should().BeNull();
    }
}

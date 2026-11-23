using FluentAssertions;
using ITMartinGallery.Domain;
using Item = ITMartinGallery.Domain.CelebrationRules.Item;

namespace ITMartinGallery.Tests;

// A year whose pictures are all on the skip list keeps its headline and tells how many were left out (2026-11-23).
public class CelebrationHiddenTests
{
    private static Item I(string name, int? year) => new(name, "image", "/u/" + name, null, year is { } y ? new DateTime(y, 6, 1) : null);

    [Test]
    public void Counts_left_out_pictures_per_year_and_undated()
    {
        var all = new[] { I("a", 2014), I("b", 2014), I("c", 2018), I("d", null), I("e", null) };
        var shown = new[] { I("c", 2018), I("e", null) };
        var hidden = CelebrationRules.HiddenPerYear(all, shown);
        hidden.Should().BeEquivalentTo(new Dictionary<string, int> { ["2014"] = 2, ["undated"] = 1 });
    }

    [Test]
    public void Nothing_hidden_gives_an_empty_map() =>
        CelebrationRules.HiddenPerYear([I("a", 2020)], [I("a", 2020)]).Should().BeEmpty();
}

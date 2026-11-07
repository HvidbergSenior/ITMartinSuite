using FluentAssertions;
using ITMartinHjem.Domain;

namespace ITMartinHjem.Tests;

// The welcome mail names only the helpers on Hjælpershoppen (user 2026-11-05: "ElPriser, Temperatur, Stem, FileSorter and Indbakke").
public class WelcomeMailTests
{
    [Test]
    public void It_lists_the_live_helpers_with_og_and_points_to_Sneboldshoppen()
    {
        var (_, body) = SignUpRules.WelcomeMail("Anna", "48810359", ["ElPriser", "Temperatur", "Stem", "FileSorter", "Indbakke"]);
        body.Should().Contain("Mine gratis snebolde: ElPriser, Temperatur, Stem, FileSorter og Indbakke.");
        body.Should().Contain("Produkter → Sneboldshoppen");
        body.Should().NotContain("Spilleder").And.NotContain("Mine snebolde.");
        body.Should().Contain("4881 0359");
    }

    [TestCase(new string[0], "")]
    [TestCase(new[] { "ElPriser" }, "ElPriser")]
    [TestCase(new[] { "ElPriser", "Stem" }, "ElPriser og Stem")]
    public void AndList_reads_like_Danish(string[] names, string expected) => SignUpRules.AndList(names).Should().Be(expected);
}

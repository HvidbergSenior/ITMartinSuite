using FluentAssertions;
using ITMartinPolstrer.Server.Services;

namespace ITMartinPolstrer.Tests.Services;

[TestFixture]
public class PieceSlugTests
{
    [Test]
    public void Lowercases_and_replaces_spaces_with_hyphens()
    {
        PieceSlug.From("Lænestol Marianne").Should().StartWith("lænestol-marianne-");
    }

    [Test]
    public void Collapses_consecutive_separators_and_trims()
    {
        var slug = PieceSlug.From("  -Sofa -- stue-  ");
        slug.Should().NotContain("--");
        slug.Should().StartWith("sofa-stue-");
    }

    [Test]
    public void Empty_title_still_gives_a_usable_folder_name()
    {
        PieceSlug.From("   ").Should().StartWith("moebel-");
    }

    [Test]
    public void Same_title_twice_gives_different_slugs()
    {
        PieceSlug.From("Lænestol").Should().NotBe(PieceSlug.From("Lænestol"));
    }
}

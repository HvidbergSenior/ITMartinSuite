using FluentAssertions;
using ITMartinPolstrer.Server.Services;

namespace ITMartinPolstrer.Tests.Services;

[TestFixture]
public class JobSlugTests
{
    [Test]
    public void Lowercases_and_replaces_spaces_with_hyphens()
    {
        var slug = JobSlug.From("Lænestol Marianne");

        slug.Should().StartWith("l");
        slug.Should().NotContain(" ");
        slug.Should().StartWith("lænestol-marianne-");
    }

    [Test]
    public void Collapses_consecutive_separators_into_one_hyphen()
    {
        var slug = JobSlug.From("Sofa -- stue");

        slug.Should().NotContain("--");
    }

    [Test]
    public void Trims_leading_and_trailing_hyphens()
    {
        var slug = JobSlug.From("  -Stol-  ");

        slug.Should().NotStartWith("-");
    }

    [Test]
    public void Two_jobs_with_the_same_title_get_different_slugs()
    {
        var first = JobSlug.From("Lænestol");
        var second = JobSlug.From("Lænestol");

        first.Should().NotBe(second, "two different chairs called the same thing must not share a NAS folder");
    }

    [Test]
    public void Preserves_digits()
    {
        var slug = JobSlug.From("Stol 2");

        slug.Should().Contain("2");
    }
}

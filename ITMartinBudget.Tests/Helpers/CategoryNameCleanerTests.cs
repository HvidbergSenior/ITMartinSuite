using FluentAssertions;
using ITMartinBudget.Application.Helpers;

namespace ITMartinBudget.Tests.Helpers;

// "the category name should not be something like BS Skat. Remove BS and
// all that" + "Cards should be named Human-friendly like Fie Tandrup not
// Debetkort Mob.Pay*Fie Tandru" (2026-09-07).
[TestFixture]
public class CategoryNameCleanerTests
{
    [Test]
    public void Strips_a_leading_BS_marker()
    {
        CategoryNameCleaner.Clean("BS Skat").Should().Be("Skat");
    }

    [Test]
    public void Strips_Debetkort_and_MobilePay_channel_prefixes()
    {
        CategoryNameCleaner.Clean("Debetkort    Mob.Pay*Fie Tandru").Should().Be("Fie Tandru");
    }

    [Test]
    public void Strips_a_doubled_MobilePay_prefix()
    {
        CategoryNameCleaner.Clean("MobilePay MobilePay Fie Tandru").Should().Be("Fie Tandru");
    }

    [Test]
    public void Strips_MobilePay_with_a_colon()
    {
        CategoryNameCleaner.Clean("MobilePay: Rema 1000").Should().Be("Rema 1000");
    }

    [Test]
    public void Strips_a_DK_NOTA_reference_prefix()
    {
        CategoryNameCleaner.Clean("DK-NOTACDKIG TELENOR.D").Should().Be("Telenor.D");
    }

    [Test]
    public void Strips_a_trailing_reference_number()
    {
        CategoryNameCleaner.Clean("Q8 Service - 8015").Should().Be("Q8 Service");
        CategoryNameCleaner.Clean("Shell Service - 92").Should().Be("Shell Service");
    }

    [Test]
    public void Title_cases_a_whole_caps_result_but_leaves_mixed_case_alone()
    {
        CategoryNameCleaner.Clean("MOBILEPAY FIE TANDRU").Should().Be("Fie Tandru");
        CategoryNameCleaner.Clean("MobilePay Fie Tandrup Rokkjær").Should().Be("Fie Tandrup Rokkjær");
    }

    [Test]
    public void Strips_a_leading_fra_marker()
    {
        // Real case 2026-09-07: a "fra Nikolaj" / "Nikolaj Lillelund Høj"
        // merge picked the shorter raw name as its target - without this,
        // that target stayed "fra Nikolaj" instead of the cleaner "Nikolaj".
        CategoryNameCleaner.Clean("fra Nikolaj").Should().Be("Nikolaj");
    }

    [Test]
    public void Leaves_an_already_clean_name_untouched()
    {
        CategoryNameCleaner.Clean("Dagligvarer").Should().Be("Dagligvarer");
        CategoryNameCleaner.Clean("Husleje").Should().Be("Husleje");
    }

    [Test]
    public void Never_returns_an_empty_string_even_if_the_whole_name_is_noise()
    {
        // "Debetkort" alone never matches the leading-prefix pattern (which
        // requires trailing whitespace + something after it), so it's left
        // as the whole-caps title-case path would apply if it were shouting -
        // here it's already mixed-case, so it comes back unchanged.
        CategoryNameCleaner.Clean("Debetkort").Should().Be("Debetkort");
        // "BS" alone (no space, nothing follows) doesn't match the BS-prefix
        // pattern either - it falls through to the whole-caps title-case
        // step like any other all-caps input.
        CategoryNameCleaner.Clean("BS").Should().Be("Bs");
    }

    [Test]
    public void Handles_null_or_whitespace_input_without_throwing()
    {
        CategoryNameCleaner.Clean("").Should().Be("");
        CategoryNameCleaner.Clean("   ").Should().Be("   ");
    }

    [Test]
    public void Collapses_double_spaces_left_over_from_the_bank_export()
    {
        CategoryNameCleaner.Clean("Debetkort    Shell Service").Should().Be("Shell Service");
    }
}

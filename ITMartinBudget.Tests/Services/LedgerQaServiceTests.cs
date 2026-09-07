using FluentAssertions;
using ITMartinBudget.Domain.Entities;
using ITMartinBudget.Domain.Enums;
using ITMartinBudget.Infrastructure.Services;

namespace ITMartinBudget.Tests.Services;

// "Tjek husleje transaktioner for 2025 og 2026. Hvad har stigningen været?"
// (2026-09-07) - the AI answered it couldn't break a category down by year,
// because BuildDigest only ever offered monthly totals ACROSS every
// category, and category totals ACROSS the whole period - nothing crossing
// category x year. This covers the "Kategorier pr. år" section added to fix
// that gap, and later "og i ai skal man ikke bruge transaktioner fra
// forretning i Privat og omvendt" - every section groups by (name, scope)
// together, never name alone, so a category never silently blends business
// and private money.
[TestFixture]
public class LedgerQaServiceTests
{
    private static BankTransaction Tx(DateTime date, decimal amount, string category, TransactionScope scope = TransactionScope.Private) =>
        new()
        {
            LedgerId = "rico",
            Date = date,
            Description = category,
            NormalizedDescription = category.ToLowerInvariant(),
            Amount = amount,
            UserCategoryName = category,
            Scope = scope,
        };

    [Test]
    public void Breaks_a_category_down_per_year_when_the_ledger_spans_multiple_years()
    {
        var transactions = new List<BankTransaction>
        {
            Tx(new DateTime(2025, 3, 1), -6603, "Husleje Privat"),
            Tx(new DateTime(2025, 9, 1), -6603, "Husleje Privat"),
            Tx(new DateTime(2026, 3, 1), -6800, "Husleje Privat"),
        };

        var digest = LedgerQaService.BuildDigest(transactions);

        digest.Should().Contain("Kategorier pr. år");
        digest.Should().Contain("Husleje Privat [Private]: 2025: -13206 kr., 2026: -6800 kr.");
    }

    [Test]
    public void Omits_the_per_year_section_entirely_for_a_ledger_spanning_only_one_year()
    {
        var transactions = new List<BankTransaction>
        {
            Tx(new DateTime(2026, 1, 1), -6603, "Husleje Privat"),
            Tx(new DateTime(2026, 3, 1), -6603, "Husleje Privat"),
        };

        var digest = LedgerQaService.BuildDigest(transactions);

        digest.Should().NotContain("Kategorier pr. år", "there's only one year - a per-year breakdown says nothing the whole-period category total doesn't already say");
    }

    [Test]
    public void A_category_only_active_in_one_of_the_years_shows_just_that_one_year()
    {
        var transactions = new List<BankTransaction>
        {
            Tx(new DateTime(2025, 6, 1), -500, "One Off 2025"),
            Tx(new DateTime(2026, 1, 1), -100, "Dagligvarer"), // some other 2026 activity to make it multi-year
        };

        var digest = LedgerQaService.BuildDigest(transactions);

        // Exactly one year listed for this category, not "2025: ..., 2026: 0".
        digest.Should().Contain("One Off 2025 [Private]: 2025: -500 kr.");
    }

    [Test]
    public void Breaks_a_top_category_down_per_month_when_it_spans_more_than_one_month()
    {
        var transactions = new List<BankTransaction>
        {
            Tx(new DateTime(2026, 1, 1), -6603, "Husleje Privat"),
            Tx(new DateTime(2026, 2, 1), -6603, "Husleje Privat"),
            Tx(new DateTime(2026, 3, 1), -6800, "Husleje Privat"),
        };

        var digest = LedgerQaService.BuildDigest(transactions);

        digest.Should().Contain("pr. måned");
        digest.Should().Contain("Husleje Privat [Private]: 2026-01: -6603 kr., 2026-02: -6603 kr., 2026-03: -6800 kr.");
    }

    [Test]
    public void Omits_the_monthly_section_when_every_top_category_only_has_one_months_activity()
    {
        var transactions = new List<BankTransaction>
        {
            Tx(new DateTime(2026, 1, 1), -500, "Engangskøb"),
        };

        var digest = LedgerQaService.BuildDigest(transactions);

        digest.Should().NotContain("pr. måned", "a single transaction in a single month has nothing a monthly breakdown would add");
    }

    [Test]
    public void Only_the_top_15_categories_by_amount_get_monthly_detail()
    {
        var transactions = new List<BankTransaction>();
        // 16 distinct multi-month categories, decreasing size - the 16th
        // (smallest) should be excluded from the monthly section.
        for (var i = 1; i <= 16; i++)
        {
            var name = $"Kategori{i:D2}";
            transactions.Add(Tx(new DateTime(2026, 1, 1), -(2000 - i * 10), name));
            transactions.Add(Tx(new DateTime(2026, 2, 1), -(2000 - i * 10), name));
        }

        var digest = LedgerQaService.BuildDigest(transactions);

        // Kategori16 legitimately still appears in the whole-period category
        // list above - only the "pr. måned" section itself is capped, so the
        // check has to look at that section specifically, not the whole digest.
        var monthlySection = digest[digest.IndexOf("pr. måned", StringComparison.Ordinal)..];
        monthlySection.Should().Contain("Kategori01 [Private]:");
        monthlySection.Should().NotContain("Kategori16 [Private]:");
    }

    [Test]
    public void Still_includes_the_whole_period_category_totals_section()
    {
        var transactions = new List<BankTransaction>
        {
            Tx(new DateTime(2025, 3, 1), -6603, "Husleje Privat"),
            Tx(new DateTime(2026, 3, 1), -6800, "Husleje Privat"),
        };

        var digest = LedgerQaService.BuildDigest(transactions);

        digest.Should().Contain("Kategorier (navn [scope]: antal poster, samlet beløb DKK):");
        digest.Should().Contain("Husleje Privat [Private]: 2 stk., -13403 kr.");
    }

    // ── "man skal ikke bruge transaktioner fra forretning i Privat og
    //    omvendt" (2026-09-07) ────────────────────────────────────────────

    [Test]
    public void A_category_name_shared_across_Business_and_Private_is_never_combined_into_one_line()
    {
        var transactions = new List<BankTransaction>
        {
            Tx(new DateTime(2026, 1, 1), -5000, "Husleje", TransactionScope.Business),
            Tx(new DateTime(2026, 1, 1), -1000, "Husleje", TransactionScope.Private),
        };

        var digest = LedgerQaService.BuildDigest(transactions);

        digest.Should().Contain("Husleje [Business]: 1 stk., -5000 kr.");
        digest.Should().Contain("Husleje [Private]: 1 stk., -1000 kr.");
        // Never a single "Husleje: 2 stk., -6000 kr." line that blends them.
        digest.Should().NotContain("-6000 kr.");
    }

    [Test]
    public void Whole_period_category_lines_are_tagged_with_their_real_scope_not_a_blended_one()
    {
        var transactions = new List<BankTransaction>
        {
            Tx(new DateTime(2026, 1, 1), 400000, "Salg Kort", TransactionScope.Business),
        };

        var digest = LedgerQaService.BuildDigest(transactions);

        digest.Should().Contain("Salg Kort [Business]:");
    }

    [Test]
    public void Per_year_and_per_month_sections_also_keep_shared_category_names_scope_separated()
    {
        var transactions = new List<BankTransaction>
        {
            Tx(new DateTime(2025, 1, 1), -5000, "Husleje", TransactionScope.Business),
            Tx(new DateTime(2026, 1, 1), -5200, "Husleje", TransactionScope.Business),
            Tx(new DateTime(2025, 1, 1), -1000, "Husleje", TransactionScope.Private),
            Tx(new DateTime(2026, 1, 1), -1100, "Husleje", TransactionScope.Private),
        };

        var digest = LedgerQaService.BuildDigest(transactions);

        digest.Should().Contain("Husleje [Business]: 2025: -5000 kr., 2026: -5200 kr.");
        digest.Should().Contain("Husleje [Private]: 2025: -1000 kr., 2026: -1100 kr.");
    }
}

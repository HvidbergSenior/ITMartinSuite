using FluentAssertions;
using ITMartinBudget.Domain.Entities;
using ITMartinBudget.Domain.Enums;
using ITMartinBudget.Infrastructure;
using ITMartinBudget.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace ITMartinBudget.Tests.Services;

// Covers GetExistingCategoryNamesAsync's standard-category union, requested
// so a brand-new ledger's category picker isn't empty (see
// StandardCategoryNames).
[TestFixture]
public class CategoryRuleServiceTests
{
    private BudgetDbContext _db = null!;
    private const string LedgerId = "bogshoppen";

    [SetUp]
    public void SetUp()
    {
        var options = new DbContextOptionsBuilder<BudgetDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new BudgetDbContext(options);
    }

    [TearDown]
    public void TearDown() => _db.Dispose();

    [Test]
    public async Task A_brand_new_ledger_with_no_saved_categories_still_offers_the_standard_starter_list()
    {
        var sut = new CategoryRuleService(_db);

        var names = await sut.GetExistingCategoryNamesAsync(LedgerId);

        names.Should().Contain(ITMartinBudget.Application.Helpers.StandardCategoryNames.Names);
    }

    [Test]
    public async Task A_real_saved_category_not_in_the_standard_list_is_still_included()
    {
        _db.CategoryRules.Add(new CategoryRule { LedgerId = LedgerId, Pattern = "flatpay", CategoryName = "Bogsalg online", Scope = TransactionScope.Business });
        await _db.SaveChangesAsync();
        var sut = new CategoryRuleService(_db);

        var names = await sut.GetExistingCategoryNamesAsync(LedgerId);

        names.Should().Contain("Bogsalg online");
    }

    [Test]
    public async Task A_saved_category_matching_a_standard_name_by_a_different_case_is_not_duplicated()
    {
        _db.CategoryRules.Add(new CategoryRule { LedgerId = LedgerId, Pattern = "løn-nov", CategoryName = "løn", Scope = TransactionScope.Private });
        await _db.SaveChangesAsync();
        var sut = new CategoryRuleService(_db);

        var names = await sut.GetExistingCategoryNamesAsync(LedgerId);

        names.Count(n => string.Equals(n, "løn", StringComparison.OrdinalIgnoreCase)).Should().Be(1);
    }

    // ── MergeCategoriesAsync (the "Flet" page's manual merge button, and the
    //    automatic clean-category-names/find-duplicate-categories passes -
    //    "men nu er alle flettet. Så tests på dette" 2026-09-07) ──────────

    [Test]
    public async Task Merging_one_source_into_a_target_renames_both_the_rule_and_its_transactions()
    {
        _db.CategoryRules.Add(new CategoryRule { LedgerId = LedgerId, Pattern = "silvan", CategoryName = "Silvan 869 Lille T", Scope = TransactionScope.Private });
        _db.Transactions.Add(new BankTransaction
        {
            LedgerId = LedgerId, NormalizedDescription = "silvan", Description = "Silvan",
            Date = new DateTime(2026, 3, 1), Amount = -465, UserCategoryName = "Silvan 869 Lille T", Scope = TransactionScope.Private,
        });
        await _db.SaveChangesAsync();
        var sut = new CategoryRuleService(_db);

        await sut.MergeCategoriesAsync(LedgerId, ["Silvan 869 Lille T"], "Dagligvarer");

        _db.CategoryRules.Single().CategoryName.Should().Be("Dagligvarer");
        _db.Transactions.Single().UserCategoryName.Should().Be("Dagligvarer");
    }

    [Test]
    public async Task Merging_several_sources_at_once_folds_every_one_into_the_same_target()
    {
        foreach (var (pattern, category) in new[] { ("silvan", "Silvan 869 Lille T"), ("expertfix", "Expertfix") })
        {
            _db.CategoryRules.Add(new CategoryRule { LedgerId = LedgerId, Pattern = pattern, CategoryName = category, Scope = TransactionScope.Private });
            _db.Transactions.Add(new BankTransaction
            {
                LedgerId = LedgerId, NormalizedDescription = pattern, Description = category,
                Date = new DateTime(2026, 3, 1), Amount = -100, UserCategoryName = category, Scope = TransactionScope.Private,
            });
        }
        await _db.SaveChangesAsync();
        var sut = new CategoryRuleService(_db);

        await sut.MergeCategoriesAsync(LedgerId, ["Silvan 869 Lille T", "Expertfix"], "Dagligvarer");

        _db.CategoryRules.Should().OnlyContain(r => r.CategoryName == "Dagligvarer");
        _db.Transactions.Should().OnlyContain(t => t.UserCategoryName == "Dagligvarer");
    }

    [Test]
    public async Task Merging_never_touches_a_category_not_named_as_a_source()
    {
        _db.CategoryRules.Add(new CategoryRule { LedgerId = LedgerId, Pattern = "husleje", CategoryName = "Husleje", Scope = TransactionScope.Business });
        _db.CategoryRules.Add(new CategoryRule { LedgerId = LedgerId, Pattern = "silvan", CategoryName = "Silvan 869 Lille T", Scope = TransactionScope.Private });
        await _db.SaveChangesAsync();
        var sut = new CategoryRuleService(_db);

        await sut.MergeCategoriesAsync(LedgerId, ["Silvan 869 Lille T"], "Dagligvarer");

        _db.CategoryRules.Single(r => r.Pattern == "husleje").CategoryName.Should().Be("Husleje");
    }

    [Test]
    public async Task Merging_never_touches_a_same_named_category_in_a_different_ledger()
    {
        _db.CategoryRules.Add(new CategoryRule { LedgerId = "hvidberg", Pattern = "silvan", CategoryName = "Silvan 869 Lille T", Scope = TransactionScope.Private });
        await _db.SaveChangesAsync();
        var sut = new CategoryRuleService(_db);

        await sut.MergeCategoriesAsync(LedgerId, ["Silvan 869 Lille T"], "Dagligvarer");

        _db.CategoryRules.Single().CategoryName.Should().Be("Silvan 869 Lille T", "the matching rule belongs to a different ledger");
    }

    // ── SetCategoryScopeAsync (the "→ Forretning"/"→ Privat" flip button -
    //    "abonnementer er forretning" 2026-09-07) ──────────────────────────

    [Test]
    public async Task Setting_a_categorys_scope_updates_both_its_rule_and_every_transaction_in_it()
    {
        _db.CategoryRules.Add(new CategoryRule { LedgerId = LedgerId, Pattern = "telia", CategoryName = "Abonnementer", Scope = TransactionScope.Private });
        _db.Transactions.Add(new BankTransaction
        {
            LedgerId = LedgerId, NormalizedDescription = "telia", Description = "Telia",
            Date = new DateTime(2026, 3, 1), Amount = -199, UserCategoryName = "Abonnementer", Scope = TransactionScope.Private,
        });
        await _db.SaveChangesAsync();
        var sut = new CategoryRuleService(_db);

        await sut.SetCategoryScopeAsync(LedgerId, "Abonnementer", TransactionScope.Business);

        _db.CategoryRules.Single().Scope.Should().Be(TransactionScope.Business);
        _db.Transactions.Single().Scope.Should().Be(TransactionScope.Business);
    }

    [Test]
    public async Task Setting_a_categorys_scope_never_touches_a_different_category()
    {
        _db.CategoryRules.Add(new CategoryRule { LedgerId = LedgerId, Pattern = "telia", CategoryName = "Abonnementer", Scope = TransactionScope.Private });
        _db.CategoryRules.Add(new CategoryRule { LedgerId = LedgerId, Pattern = "husleje", CategoryName = "Husleje", Scope = TransactionScope.Private });
        await _db.SaveChangesAsync();
        var sut = new CategoryRuleService(_db);

        await sut.SetCategoryScopeAsync(LedgerId, "Abonnementer", TransactionScope.Business);

        _db.CategoryRules.Single(r => r.Pattern == "husleje").Scope.Should().Be(TransactionScope.Private);
    }
}

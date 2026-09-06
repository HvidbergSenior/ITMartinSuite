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
}

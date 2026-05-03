using ITMartinBudget.Domain.Entities;
using ITMartinBudget.Domain.Enums;

namespace ITMartin.Budget.Tests.Domain;

[TestFixture]
public class CategoryEngineTests
{
    [Test]
    public void Detect_Should_Return_Matching_Category()
    {
        var rules = new List<CategoryRule>
        {
            new CategoryRule
            {
                Keyword = "netflix",
                SubCategory = SubCategory.StreamingTjenester
            }
        };

        var engine = new CategoryEngine(rules);

        var result = engine.Detect("netflix subscription");

        Assert.That(result, Is.EqualTo(SubCategory.StreamingTjenester));
    }

    [Test]
    public void Detect_Should_Be_Case_Insensitive()
    {
        var rules = new List<CategoryRule>
        {
            new CategoryRule
            {
                Keyword = "netflix",
                SubCategory = SubCategory.StreamingTjenester
            }
        };

        var engine = new CategoryEngine(rules);

        var result = engine.Detect("NETFLIX PAYMENT");

        Assert.That(result, Is.EqualTo(SubCategory.StreamingTjenester));
    }

    [Test]
    public void Detect_Should_Return_Ukendt_When_No_Match()
    {
        var rules = new List<CategoryRule>();

        var engine = new CategoryEngine(rules);

        var result = engine.Detect("random text");

        Assert.That(result, Is.EqualTo(SubCategory.Ukendt));
    }

    [Test]
    public void Detect_Should_Respect_Multiple_Rules()
    {
        var rules = new List<CategoryRule>
        {
            new CategoryRule
            {
                Keyword = "netflix",
                SubCategory = SubCategory.StreamingTjenester
            },
            new CategoryRule
            {
                Keyword = "restaurant",
                SubCategory = SubCategory.Restaurant
            }
        };

        var engine = new CategoryEngine(rules);

        var result = engine.Detect("restaurant visit");

        Assert.That(result, Is.EqualTo(SubCategory.Restaurant));
    }
}
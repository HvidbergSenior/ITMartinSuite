using ITMartinBudget.Application.Services;
using ITMartinBudget.Domain.Enums;

namespace ITMartin.Budget.Tests.Domain;

[TestFixture]
public class CategoryMapperTests
{
    [Test]
    public void Map_Should_Map_Streaming_To_Abonnement()
    {
        var result = CategoryMapper.Map(SubCategory.StreamingTjenester);

        Assert.That(result, Is.EqualTo(Category.Abonnement));
    }

    [Test]
    public void Map_Should_Map_Dagligvarer_To_Mad()
    {
        var result = CategoryMapper.Map(SubCategory.Dagligvarer);

        Assert.That(result, Is.EqualTo(Category.Mad));
    }

    [Test]
    public void Map_Should_Map_Benzin_To_Transport()
    {
        var result = CategoryMapper.Map(SubCategory.Benzin);

        Assert.That(result, Is.EqualTo(Category.Transport));
    }

    [Test]
    public void Map_Should_Map_Ukendt_To_Andet()
    {
        var result = CategoryMapper.Map(SubCategory.Ukendt);

        Assert.That(result, Is.EqualTo(Category.Andet));
    }
}
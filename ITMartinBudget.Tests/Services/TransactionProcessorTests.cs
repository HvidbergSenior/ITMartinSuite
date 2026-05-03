using ITMartinBudget.Application.Interfaces;
using ITMartinBudget.Application.Services;
using ITMartinBudget.Domain.Entities;
using ITMartinBudget.Domain.Enums;
using Moq;

namespace ITMartin.Budget.Tests.Services;

[TestFixture]
public class TransactionProcessorTests
{
    private Mock<ITransactionGroupingService> _groupingMock;
    private Mock<ICategoryService> _categoryMock;
    private Mock<INameNormalizer> _nameMock;

    private TransactionProcessor _processor;

    [SetUp]
    public void Setup()
    {
        _groupingMock = new Mock<ITransactionGroupingService>();
        _categoryMock = new Mock<ICategoryService>();
        _nameMock = new Mock<INameNormalizer>();

        _groupingMock
            .Setup(x => x.GetGroupingKey(It.IsAny<BankTransaction>()))
            .Returns("group");

        _categoryMock
            .Setup(x => x.DetectAsync(It.IsAny<string>(), It.IsAny<BankTransaction>()))
            .ReturnsAsync(SubCategory.Dagligvarer);

        _processor = new TransactionProcessor(
            _groupingMock.Object,
            _categoryMock.Object,
            _nameMock.Object);
    }

    [Test]
    public async Task ProcessAsync_Should_Set_Grouping_And_Category()
    {
        var tx = new BankTransaction
        {
            Description = "test",
            Amount = -100
        };

        await _processor.ProcessAsync(tx);

        Assert.That(tx.NormalizedDescription, Is.EqualTo("group"));
        Assert.That(tx.SubCategory, Is.EqualTo(SubCategory.Dagligvarer));
    }

    [Test]
    public async Task ProcessAsync_Should_Handle_MobilePay()
    {
        var tx = new BankTransaction
        {
            Description = "mobilepay john",
            Amount = 100
        };

        _categoryMock
            .Setup(x => x.DetectAsync(It.IsAny<string>(), It.IsAny<BankTransaction>()))
            .ReturnsAsync(SubCategory.Ukendt);

        _nameMock
            .Setup(x => x.Normalize(It.IsAny<string>()))
            .Returns("John");

        await _processor.ProcessAsync(tx);

        Assert.That(tx.MobilePayName, Is.EqualTo("John"));
    }
}
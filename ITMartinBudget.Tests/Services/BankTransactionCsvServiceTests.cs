using System.Text;
using ITMartinBudget.Application.Interfaces;
using ITMartinBudget.Domain.Entities;
using ITMartinBudget.Infrastructure;
using ITMartinBudget.Infrastructure.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace ITMartin.Budget.Tests.Services;

[TestFixture]
public class BankTransactionCsvServiceTests
{
    private BudgetDbContext _db;
    private SqliteConnection _connection;
    private Mock<ITransactionProcessor> _processorMock;
    private BankTransactionCsvService _service;

    [SetUp]
    public void Setup()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<BudgetDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new BudgetDbContext(options);
        _db.Database.EnsureCreated();

        _processorMock = new Mock<ITransactionProcessor>();

        _processorMock
            .Setup(x => x.ProcessAsync(It.IsAny<BankTransaction>()))
            .Returns(Task.CompletedTask);

        _service = new BankTransactionCsvService(
            _db,
            _processorMock.Object);
    }

    [TearDown]
    public void TearDown()
    {
        _db.Dispose();
        _connection.Close();
    }

    [Test]
    public async Task ImportAsync_Should_Save_New_Transactions()
    {
        var csv =
            "Dato;Tekst;Beløb;Saldo;Afstemt;Kontonummer;Kontonavn;Hovedkategori;Kategori;Kommentar\n" +
            "01.01.2024;Test;100;0;;123;Testkonto;Andet;Anden udgift;";
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        var result = await _service.ImportAsync(stream);

        Assert.That(result.Count, Is.EqualTo(1));
        Assert.That(_db.Transactions.Count(), Is.EqualTo(1));

        _processorMock.Verify(x =>
                x.ProcessAsync(It.IsAny<BankTransaction>()),
            Times.Once);
    }

    [Test]
    public async Task ImportAsync_Should_Not_Insert_Duplicates()
    {
        var existing = new BankTransaction
        {
            Date = new DateTime(2024, 1, 1),
            Amount = 100,
            NormalizedDescription = "group"
        };

        _db.Transactions.Add(existing);
        await _db.SaveChangesAsync();

        _processorMock
            .Setup(x => x.ProcessAsync(It.IsAny<BankTransaction>()))
            .Callback<BankTransaction>(tx =>
            {
                tx.NormalizedDescription = "group";
            })
            .Returns(Task.CompletedTask);

        var csv =
            "Dato;Tekst;Beløb;Saldo;Afstemt;Kontonummer;Kontonavn;Hovedkategori;Kategori;Kommentar\n" +
            "01.01.2024;Test;100;0;;123;Testkonto;Andet;Anden udgift;";
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        var result = await _service.ImportAsync(stream);

        Assert.That(result.Count, Is.EqualTo(0));
    }
}
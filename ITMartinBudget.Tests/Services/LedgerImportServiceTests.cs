using System.Text;
using FluentAssertions;
using ITMartinBudget.Application.Interfaces;
using ITMartinBudget.Domain.Entities;
using ITMartinBudget.Domain.Enums;
using ITMartinBudget.Infrastructure;
using ITMartinBudget.Infrastructure.Csv;
using ITMartinBudget.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace ITMartinBudget.Tests.Services;

// Step 1 of the Bogshoppen workflow ("Upload a csv") - the entire import
// pipeline every bank format funnels through: scope classification, saved
// category-rule application/seeding, and dedup. Previously zero coverage.
[TestFixture]
public class LedgerImportServiceTests
{
    private BudgetDbContext _db = null!;
    private Mock<ITransactionScopeClassifier> _scopeClassifier = null!;
    private Mock<ITransactionCategorizer> _categorizer = null!;
    private const string LedgerId = "bogshoppen";

    [SetUp]
    public void SetUp()
    {
        var options = new DbContextOptionsBuilder<BudgetDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new BudgetDbContext(options);
        _scopeClassifier = new Mock<ITransactionScopeClassifier>();
        _categorizer = new Mock<ITransactionCategorizer>();
    }

    [TearDown]
    public void TearDown() => _db.Dispose();

    private LedgerImportService CreateSut() =>
        new(_db, _scopeClassifier.Object, _categorizer.Object, [new RawBankStatementParser()]);

    private static Stream RawCsv(params string[] rows) =>
        new MemoryStream(Encoding.GetEncoding(1252).GetBytes(string.Join("\r\n", rows) + "\r\n"));

    private const string OneRow = "1;2;3;06-09-2026;Flatpay udbetaling;15000,00;15000,00;;;";

    // ── Scope resolution ─────────────────────────────────────────────────

    [Test]
    public async Task A_ledger_with_no_config_defaults_to_Both_and_runs_the_scope_classifier()
    {
        var sut = CreateSut();

        await sut.ImportAsync(RawCsv(OneRow), LedgerId);

        _scopeClassifier.Verify(c => c.Classify(It.IsAny<BankTransaction>()), Times.Once);
    }

    [Test]
    public async Task A_BusinessOnly_ledger_never_calls_the_scope_classifier_and_hard_sets_Business()
    {
        _db.LedgerConfigs.Add(new LedgerConfig { LedgerId = LedgerId, ScopeMode = LedgerScopeMode.BusinessOnly });
        await _db.SaveChangesAsync();
        var sut = CreateSut();

        var imported = await sut.ImportAsync(RawCsv(OneRow), LedgerId);

        _scopeClassifier.Verify(c => c.Classify(It.IsAny<BankTransaction>()), Times.Never);
        imported.Single().Scope.Should().Be(TransactionScope.Business);
    }

    [Test]
    public async Task A_PrivateOnly_ledger_never_calls_the_scope_classifier_and_hard_sets_Private()
    {
        _db.LedgerConfigs.Add(new LedgerConfig { LedgerId = LedgerId, ScopeMode = LedgerScopeMode.PrivateOnly });
        await _db.SaveChangesAsync();
        var sut = CreateSut();

        var imported = await sut.ImportAsync(RawCsv(OneRow), LedgerId);

        _scopeClassifier.Verify(c => c.Classify(It.IsAny<BankTransaction>()), Times.Never);
        imported.Single().Scope.Should().Be(TransactionScope.Private);
    }

    [Test]
    public async Task A_Both_mode_ledger_runs_the_scope_classifier_for_every_row()
    {
        _db.LedgerConfigs.Add(new LedgerConfig { LedgerId = LedgerId, ScopeMode = LedgerScopeMode.Both });
        await _db.SaveChangesAsync();
        var sut = CreateSut();

        await sut.ImportAsync(RawCsv(
            "1;2;3;01-02-2026;Flatpay udbetaling;15000,00;15000,00;;;",
            "1;2;3;15-02-2026;NewSec husleje;-5000,00;10000,00;;;"), LedgerId);

        _scopeClassifier.Verify(c => c.Classify(It.IsAny<BankTransaction>()), Times.Exactly(2));
    }

    // ── Private-scope rows also go through the family categorizer ──────────

    [Test]
    public async Task A_row_the_classifier_marks_Private_is_also_run_through_the_categorizer()
    {
        _scopeClassifier.Setup(c => c.Classify(It.IsAny<BankTransaction>()))
            .Callback<BankTransaction>(t => t.Scope = TransactionScope.Private);
        var sut = CreateSut();

        await sut.ImportAsync(RawCsv(OneRow), LedgerId);

        _categorizer.Verify(c => c.Categorize(It.IsAny<BankTransaction>()), Times.Once);
    }

    [Test]
    public async Task A_row_the_classifier_marks_Business_never_reaches_the_categorizer()
    {
        _scopeClassifier.Setup(c => c.Classify(It.IsAny<BankTransaction>()))
            .Callback<BankTransaction>(t => t.Scope = TransactionScope.Business);
        var sut = CreateSut();

        await sut.ImportAsync(RawCsv(OneRow), LedgerId);

        _categorizer.Verify(c => c.Categorize(It.IsAny<BankTransaction>()), Times.Never);
    }

    // ── Dedup ────────────────────────────────────────────────────────────

    [Test]
    public async Task Importing_the_same_file_twice_does_not_duplicate_rows()
    {
        var sut = CreateSut();

        await sut.ImportAsync(RawCsv(OneRow), LedgerId);
        var secondImport = await sut.ImportAsync(RawCsv(OneRow), LedgerId);

        secondImport.Should().BeEmpty("every row in this file was already imported");
        _db.Transactions.Count(x => x.LedgerId == LedgerId).Should().Be(1);
    }

    [Test]
    public async Task Re_uploading_an_overlapping_export_only_imports_the_genuinely_new_rows()
    {
        var sut = CreateSut();
        await sut.ImportAsync(RawCsv(
            "1;2;3;01-02-2026;Flatpay udbetaling;15000,00;15000,00;;;",
            "1;2;3;15-02-2026;NewSec husleje;-5000,00;10000,00;;;"), LedgerId);

        var secondImport = await sut.ImportAsync(RawCsv(
            "1;2;3;15-02-2026;NewSec husleje;-5000,00;10000,00;;;",   // already imported
            "1;2;3;20-02-2026;Rema 1000;-800,00;9200,00;;;"), LedgerId);          // genuinely new

        secondImport.Should().HaveCount(1);
        secondImport[0].Description.Should().Be("Rema 1000");
        _db.Transactions.Count(x => x.LedgerId == LedgerId).Should().Be(3);
    }

    [Test]
    public async Task Two_different_ledgers_can_have_an_identical_looking_row_without_colliding()
    {
        var sut = CreateSut();

        await sut.ImportAsync(RawCsv(OneRow), "bogshoppen");
        var secondLedgerImport = await sut.ImportAsync(RawCsv(OneRow), "hvidberg");

        secondLedgerImport.Should().HaveCount(1, "dedup is scoped per ledger, not global");
    }

    [Test]
    public async Task Known_gap_two_genuinely_different_same_day_same_amount_transactions_collide()
    {
        // Documents the real limitation found during review: the dedup key
        // is date + amount + normalized description with no time-of-day and
        // no running index, so two distinct sales of the same amount to a
        // similarly-described counterparty on the same day are
        // indistinguishable and the second is silently dropped. This test
        // exists to make that behavior visible and break loudly if the key
        // ever changes shape, not to assert it's correct.
        var sut = CreateSut();
        await sut.ImportAsync(RawCsv("1;2;3;06-09-2026;Kunde betaling;500,00;500,00;;;"), LedgerId);

        var secondSale = await sut.ImportAsync(RawCsv("1;2;3;06-09-2026;Kunde betaling;500,00;1000,00;;;"), LedgerId);

        secondSale.Should().BeEmpty("current key ignores Balance and time-of-day, so this second real sale is dropped");
        _db.Transactions.Count(x => x.LedgerId == LedgerId).Should().Be(1);
    }

    // ── Saved category rules ─────────────────────────────────────────────

    [Test]
    public async Task A_saved_rule_overrides_the_classifiers_scope_and_sets_the_category()
    {
        _db.CategoryRules.Add(new CategoryRule
        {
            LedgerId = LedgerId,
            Pattern = ITMartinBudget.Application.Helpers.TransactionNormalizer.Normalize("Flatpay udbetaling"),
            CategoryName = "Bogsalg online",
            Scope = TransactionScope.Business,
        });
        await _db.SaveChangesAsync();
        _scopeClassifier.Setup(c => c.Classify(It.IsAny<BankTransaction>()))
            .Callback<BankTransaction>(t => t.Scope = TransactionScope.Unknown);
        var sut = CreateSut();

        var imported = await sut.ImportAsync(RawCsv(OneRow), LedgerId);

        imported.Single().Scope.Should().Be(TransactionScope.Business);
        imported.Single().UserCategoryName.Should().Be("Bogsalg online");
    }

    [Test]
    public async Task A_saved_rule_is_applied_automatically_to_every_future_import_not_just_the_next_one()
    {
        _db.CategoryRules.Add(new CategoryRule
        {
            LedgerId = LedgerId,
            Pattern = ITMartinBudget.Application.Helpers.TransactionNormalizer.Normalize("NewSec husleje"),
            CategoryName = "Husleje",
            Scope = TransactionScope.Business,
        });
        await _db.SaveChangesAsync();
        var sut = CreateSut();

        var imported = await sut.ImportAsync(RawCsv("1;2;3;15-03-2026;NewSec husleje;-5000,00;10000,00;;;"), LedgerId);

        imported.Single().UserCategoryName.Should().Be("Husleje");
    }

    // ── Bank-provided category (Totalkonto-style formats only) ──────────────

    [Test]
    public async Task A_bank_provided_category_seeds_a_new_rule_when_none_exists_yet()
    {
        var sut = new LedgerImportService(_db, _scopeClassifier.Object, _categorizer.Object,
            [new TotalkontoParser()]);
        var csv = new MemoryStream(Encoding.UTF8.GetBytes(
            "Dato;Tekst;Beløb;Saldo;Hovedkategori;Kategori;Kommentar\r\n" +
            "06.09.2026;Netto;-450,00;1000,00;Privat;Dagligvarer;\r\n"));

        var imported = await sut.ImportAsync(csv, "hvidberg");

        imported.Single().UserCategoryName.Should().Be("Dagligvarer");
        _db.CategoryRules.Should().ContainSingle(r => r.CategoryName == "Dagligvarer" && r.LedgerId == "hvidberg");
    }

    [Test]
    public async Task Bogshoppens_raw_format_never_seeds_a_rule_from_a_bank_category_since_it_has_none()
    {
        var sut = CreateSut();

        await sut.ImportAsync(RawCsv(OneRow), LedgerId);

        _db.CategoryRules.Should().BeEmpty();
    }

    // ── Malformed/unrecognized input ────────────────────────────────────

    [Test]
    public async Task An_unrecognized_csv_shape_throws_a_clear_error_instead_of_the_raw_parser_exception()
    {
        // Reproduces a real production bug (2026-09-07): uploading a
        // pre-cleaned "Date,Description,Amount,Balance,RawDetails" export
        // (comma-delimited, has a header) fell through to
        // RawBankStatementParser - which matches anything - and threw an
        // unhandled CsvHelper.TypeConversion.TypeConverterException trying
        // to read the header row itself as a data row. That leaked as a 500
        // with no useful message; this asserts it now surfaces as one clear,
        // catchable exception instead.
        var sut = CreateSut();
        var garbage = new MemoryStream(Encoding.UTF8.GetBytes(
            "Date,Description,Amount,Balance,RawDetails\r\n" +
            "\"2026-01-02 00:00:00\",\"BS MODSTRØM DANMARK A/S\",-1109.88,69776.28,\"\"\r\n"));

        var act = async () => await sut.ImportAsync(garbage, LedgerId);

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*ikke*genkendt format*");
        _db.Transactions.Should().BeEmpty("a failed parse must not leave partial rows behind");
    }

    // ── Return value ─────────────────────────────────────────────────────

    [Test]
    public async Task ImportAsync_returns_only_the_newly_inserted_rows_not_the_whole_ledger()
    {
        var sut = CreateSut();
        await sut.ImportAsync(RawCsv("1;2;3;01-02-2026;Flatpay udbetaling;15000,00;15000,00;;;"), LedgerId);

        var secondImport = await sut.ImportAsync(RawCsv(
            "1;2;3;01-02-2026;Flatpay udbetaling;15000,00;15000,00;;;",
            "1;2;3;15-02-2026;NewSec husleje;-5000,00;10000,00;;;"), LedgerId);

        secondImport.Should().HaveCount(1);
        secondImport[0].Description.Should().Be("NewSec husleje");
    }
}

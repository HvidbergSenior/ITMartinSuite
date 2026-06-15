using FluentAssertions;
using ITMartin.Ai.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Receipt.Application.Interfaces;
using ITMartin.Receipt.Application.Workflows;
using ITMartin.Receipt.Application.Workflows.Steps;
using Moq;

namespace ITMartin.Receipt.Tests.Workflows.Steps;

[TestFixture]
public class SaveTransactionWorkflowStepTests
{
    private Mock<IReceiptRepository> _repository = null!;
    private SaveTransactionWorkflowStep _step = null!;

    [SetUp]
    public void SetUp()
    {
        _repository = new Mock<IReceiptRepository>();
        _step = new SaveTransactionWorkflowStep(_repository.Object);
    }

    private static WorkflowExecutionContext<ReceiptContext> Context(
        ReceiptContext state) =>
        new()
        {
            WorkflowId = Guid.NewGuid(),
            WorkflowName = "TestWorkflow",
            State = state
        };

    private static ReceiptContext ContextWithExtraction(
        string merchant = "Netto",
        string currency = "DKK",
        decimal total = 99.95m,
        decimal vat = 24.99m,
        DateTime? date = null,
        List<ReceiptLineItem>? items = null) =>
        new()
        {
            ImagePath = "/tmp/receipt.jpg",
            ExtractionResult = new ReceiptExtractionResult
            {
                MerchantName = merchant,
                Currency = currency,
                TotalAmount = total,
                VatAmount = vat,
                PurchaseDate = date ?? new DateTime(2024, 3, 15),
                Items = items ?? []
            }
        };

    // =====================================
    // Guard: missing extraction result
    // =====================================

    [Test]
    public async Task Throws_when_extraction_result_is_null()
    {
        var state = new ReceiptContext { ImagePath = "/tmp/receipt.jpg" };

        var act = () => _step.ExecuteAsync(Context(state));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Extraction result missing*");
    }

    // =====================================
    // Application transaction mapping
    // =====================================

    [Test]
    public async Task Sets_application_transaction_on_context()
    {
        var state = ContextWithExtraction();

        await _step.ExecuteAsync(Context(state));

        state.Transaction.Should().NotBeNull();
    }

    [Test]
    public async Task Maps_merchant_name_to_application_transaction()
    {
        var state = ContextWithExtraction(merchant: "Bilka Aarhus");

        await _step.ExecuteAsync(Context(state));

        state.Transaction!.MerchantName.Should().Be("Bilka Aarhus");
    }

    [Test]
    public async Task Null_merchant_name_becomes_empty_string_in_application_transaction()
    {
        var state = new ReceiptContext
        {
            ImagePath = "/tmp/receipt.jpg",
            ExtractionResult = new ReceiptExtractionResult
            {
                MerchantName = null,
                Currency = "DKK"
            }
        };

        await _step.ExecuteAsync(Context(state));

        state.Transaction!.MerchantName.Should().Be(string.Empty);
    }

    [Test]
    public async Task Null_currency_defaults_to_DKK_in_application_transaction()
    {
        var state = new ReceiptContext
        {
            ImagePath = "/tmp/receipt.jpg",
            ExtractionResult = new ReceiptExtractionResult { Currency = null }
        };

        await _step.ExecuteAsync(Context(state));

        state.Transaction!.Currency.Should().Be("DKK");
    }

    [Test]
    public async Task Maps_total_vat_and_date_to_application_transaction()
    {
        var date = new DateTime(2024, 6, 1);
        var state = ContextWithExtraction(total: 250m, vat: 50m, date: date);

        await _step.ExecuteAsync(Context(state));

        state.Transaction!.TotalAmount.Should().Be(250m);
        state.Transaction!.VatAmount.Should().Be(50m);
        state.Transaction!.PurchaseDate.Should().Be(date);
    }

    [Test]
    public async Task Maps_line_items_to_application_transaction()
    {
        var state = ContextWithExtraction(items:
        [
            new ReceiptLineItem { Description = "Mælk 1L", Amount = 12.95m },
            new ReceiptLineItem { Description = "Brød", Amount = 24.50m }
        ]);

        await _step.ExecuteAsync(Context(state));

        state.Transaction!.Items.Should().HaveCount(2);
        state.Transaction!.Items[0].Description.Should().Be("Mælk 1L");
        state.Transaction!.Items[0].Amount.Should().Be(12.95m);
        state.Transaction!.Items[1].Description.Should().Be("Brød");
    }

    [Test]
    public async Task Application_transaction_has_non_empty_id()
    {
        var state = ContextWithExtraction();

        await _step.ExecuteAsync(Context(state));

        state.Transaction!.Id.Should().NotBe(Guid.Empty);
    }

    // =====================================
    // Domain entity persistence
    // =====================================

    [Test]
    public async Task Calls_repository_save_once()
    {
        var state = ContextWithExtraction();

        await _step.ExecuteAsync(Context(state));

        _repository.Verify(
            r => r.SaveAsync(It.IsAny<Domain.Entities.ReceiptTransaction>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task Domain_transaction_has_same_id_as_application_transaction()
    {
        Domain.Entities.ReceiptTransaction? captured = null;
        _repository
            .Setup(r => r.SaveAsync(It.IsAny<Domain.Entities.ReceiptTransaction>(), It.IsAny<CancellationToken>()))
            .Callback<Domain.Entities.ReceiptTransaction, CancellationToken>((t, _) => captured = t);

        var state = ContextWithExtraction();

        await _step.ExecuteAsync(Context(state));

        captured!.Id.Should().Be(state.Transaction!.Id);
    }

    [Test]
    public async Task Domain_transaction_merchant_matches_application_transaction()
    {
        Domain.Entities.ReceiptTransaction? captured = null;
        _repository
            .Setup(r => r.SaveAsync(It.IsAny<Domain.Entities.ReceiptTransaction>(), It.IsAny<CancellationToken>()))
            .Callback<Domain.Entities.ReceiptTransaction, CancellationToken>((t, _) => captured = t);

        var state = ContextWithExtraction(merchant: "Føtex");

        await _step.ExecuteAsync(Context(state));

        captured!.MerchantName.Should().Be("Føtex");
    }

    [Test]
    public async Task Domain_transaction_items_match_extraction_items()
    {
        Domain.Entities.ReceiptTransaction? captured = null;
        _repository
            .Setup(r => r.SaveAsync(It.IsAny<Domain.Entities.ReceiptTransaction>(), It.IsAny<CancellationToken>()))
            .Callback<Domain.Entities.ReceiptTransaction, CancellationToken>((t, _) => captured = t);

        var state = ContextWithExtraction(items:
        [
            new ReceiptLineItem { Description = "Smør", Amount = 19.95m }
        ]);

        await _step.ExecuteAsync(Context(state));

        captured!.Items.Should().HaveCount(1);
        captured!.Items[0].Description.Should().Be("Smør");
        captured!.Items[0].Amount.Should().Be(19.95m);
    }

    [Test]
    public async Task Null_currency_defaults_to_DKK_in_domain_transaction()
    {
        Domain.Entities.ReceiptTransaction? captured = null;
        _repository
            .Setup(r => r.SaveAsync(It.IsAny<Domain.Entities.ReceiptTransaction>(), It.IsAny<CancellationToken>()))
            .Callback<Domain.Entities.ReceiptTransaction, CancellationToken>((t, _) => captured = t);

        var state = new ReceiptContext
        {
            ImagePath = "/tmp/receipt.jpg",
            ExtractionResult = new ReceiptExtractionResult { Currency = null }
        };

        await _step.ExecuteAsync(Context(state));

        captured!.Currency.Should().Be("DKK");
    }

    // =====================================
    // Edge cases
    // =====================================

    [Test]
    public async Task Empty_items_list_is_handled_gracefully()
    {
        var state = ContextWithExtraction(items: []);

        await _step.ExecuteAsync(Context(state));

        state.Transaction!.Items.Should().BeEmpty();
    }

    [Test]
    public async Task Passes_cancellation_token_to_repository()
    {
        using var cts = new CancellationTokenSource();
        var token = cts.Token;

        var state = ContextWithExtraction();

        await _step.ExecuteAsync(Context(state), token);

        _repository.Verify(
            r => r.SaveAsync(It.IsAny<Domain.Entities.ReceiptTransaction>(), token),
            Times.Once);
    }
}

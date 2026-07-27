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
        string? date = null,
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
                PurchaseDate = date ?? "2024-03-15",
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
        var date = "2024-06-01";
        var state = ContextWithExtraction(total: 250m, vat: 50m, date: date);

        await _step.ExecuteAsync(Context(state));

        state.Transaction!.TotalAmount.Should().Be(250m);
        state.Transaction!.VatAmount.Should().Be(50m);
        state.Transaction!.PurchaseDate.Should().Be(DateTime.Parse(date));
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
        state.Transaction!.Items[0].OriginalPrice.Should().Be(12.95m);
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
        captured!.Items[0].OriginalPrice.Should().Be(19.95m);
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

    // =====================================
    // Auto-learning (IsTemplate) - a scan with no suspicious items whose
    // item total reconciles with the printed total becomes this merchant's
    // new reference example, with no user action. Different receipt
    // shapes exercising each condition of that eligibility rule -
    // including the exact failure mode from the real JYSK bug (an
    // extraction with zero items) to confirm it correctly never became a
    // template on its own.
    // =====================================

    private async Task<Domain.Entities.ReceiptTransaction> RunAndCapture(ReceiptContext state)
    {
        Domain.Entities.ReceiptTransaction? captured = null;
        _repository
            .Setup(r => r.SaveAsync(It.IsAny<Domain.Entities.ReceiptTransaction>(), It.IsAny<CancellationToken>()))
            .Callback<Domain.Entities.ReceiptTransaction, CancellationToken>((t, _) => captured = t);

        await _step.ExecuteAsync(Context(state));

        return captured!;
    }

    [Test]
    public async Task Clean_reconciling_receipt_becomes_a_template()
    {
        var state = ContextWithExtraction(total: 50.40m, items:
        [
            new ReceiptLineItem { Description = "Mælk 1L", Amount = 12.95m },
            new ReceiptLineItem { Description = "Rugbrød", Amount = 24.50m },
            new ReceiptLineItem { Description = "Bananer 1 kg", Amount = 12.95m }
        ]);

        var captured = await RunAndCapture(state);

        captured.IsTemplate.Should().BeTrue();
    }

    [Test]
    public async Task Receipt_with_a_suspicious_item_never_becomes_a_template()
    {
        // Math checks out (100 = 100), but a flagged item should still
        // block auto-learning - the whole point of "suspicious" is that a
        // human should look at it before it's trusted as a reference.
        var state = ContextWithExtraction(total: 100m, items:
        [
            new ReceiptLineItem { Description = "Bananer", Amount = 100m, Suspicious = true }
        ]);

        var captured = await RunAndCapture(state);

        captured.IsTemplate.Should().BeFalse();
    }

    [Test]
    public async Task Receipt_whose_item_total_does_not_reconcile_never_becomes_a_template()
    {
        var state = ContextWithExtraction(total: 999m, items:
        [
            new ReceiptLineItem { Description = "Mælk", Amount = 12.95m }
        ]);

        var captured = await RunAndCapture(state);

        captured.IsTemplate.Should().BeFalse();
    }

    [Test]
    public async Task Zero_items_never_becomes_a_template()
    {
        // The exact real-world failure mode this guards: the JYSK scan's
        // header (merchant/date/total) extracted correctly but every line
        // item was lost to a bad-OCR bug - this must never silently become
        // the new "correct" reference for JYSK receipts going forward.
        var state = ContextWithExtraction(total: 3359.20m, items: []);

        var captured = await RunAndCapture(state);

        captured.IsTemplate.Should().BeFalse();
    }

    [Test]
    public async Task Blank_merchant_name_never_becomes_a_template()
    {
        var state = new ReceiptContext
        {
            ImagePath = "/tmp/receipt.jpg",
            ExtractionResult = new ReceiptExtractionResult
            {
                MerchantName = "",
                Currency = "DKK",
                TotalAmount = 12.95m,
                Items = [new ReceiptLineItem { Description = "Mælk", Amount = 12.95m }]
            }
        };

        var captured = await RunAndCapture(state);

        captured.IsTemplate.Should().BeFalse();
    }

    [Test]
    public async Task Missing_total_reconciles_automatically_and_can_still_become_a_template()
    {
        // No printed total to check items against - the rule treats that
        // as "nothing to contradict", not as a reason to distrust the scan.
        var state = new ReceiptContext
        {
            ImagePath = "/tmp/receipt.jpg",
            ExtractionResult = new ReceiptExtractionResult
            {
                MerchantName = "Netto",
                Currency = "DKK",
                TotalAmount = null,
                Items = [new ReceiptLineItem { Description = "Mælk", Amount = 12.95m }]
            }
        };

        var captured = await RunAndCapture(state);

        captured.IsTemplate.Should().BeTrue();
    }

    [Test]
    public async Task Reconciliation_includes_discounts_not_just_original_price()
    {
        // itemsNet = OriginalPrice + DiscountAmount (a negative number) -
        // a discounted item must reconcile against the DISCOUNTED total,
        // not the pre-discount price.
        var state = ContextWithExtraction(total: 100m, items:
        [
            new ReceiptLineItem { Description = "Jakke", Amount = 150m, DiscountAmount = -50m }
        ]);

        var captured = await RunAndCapture(state);

        captured.IsTemplate.Should().BeTrue();
    }

    [Test]
    public async Task Reconciliation_within_one_krone_tolerance_still_becomes_a_template()
    {
        var state = ContextWithExtraction(total: 51.00m, items:
        [
            new ReceiptLineItem { Description = "Mælk", Amount = 12.95m },
            new ReceiptLineItem { Description = "Rugbrød", Amount = 24.50m },
            new ReceiptLineItem { Description = "Bananer", Amount = 12.95m }
        ]); // items sum to 50.40 - exactly 0.60 off the printed 51.00

        var captured = await RunAndCapture(state);

        captured.IsTemplate.Should().BeTrue();
    }

    [Test]
    public async Task Reconciliation_just_outside_tolerance_never_becomes_a_template()
    {
        var state = ContextWithExtraction(total: 52.41m, items:
        [
            new ReceiptLineItem { Description = "Mælk", Amount = 12.95m },
            new ReceiptLineItem { Description = "Rugbrød", Amount = 24.50m },
            new ReceiptLineItem { Description = "Bananer", Amount = 12.95m }
        ]); // items sum to 50.40 - 2.01 off the printed 52.41, past the 1.0 tolerance

        var captured = await RunAndCapture(state);

        captured.IsTemplate.Should().BeFalse();
    }
}

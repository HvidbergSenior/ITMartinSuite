using FluentAssertions;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.OCR.Interfaces;
using ITMartin.Receipt.Application.Workflows;
using ITMartin.Receipt.Application.Workflows.Steps;
using Moq;

namespace ITMartin.Receipt.Tests.Workflows.Steps;

// Covers the OCR-plausibility gate added after a real bug: a photographed
// (not scanned) JYSK receipt let Tesseract "succeed" (no exception) with
// text that read the large, bold header/total fine but lost every small
// line-item row - so the code trusted it instead of falling back to sending
// the image straight to Claude. These scenarios cover the kinds of OCR
// output that should, and shouldn't, be trusted.
[TestFixture]
public class ReceiptOcrWorkflowStepTests
{
    private Mock<IGeneralOcrService> _ocr = null!;
    private ReceiptOcrWorkflowStep _step = null!;

    [SetUp]
    public void SetUp()
    {
        _ocr = new Mock<IGeneralOcrService>();
        _step = new ReceiptOcrWorkflowStep(_ocr.Object);
    }

    private static WorkflowExecutionContext<ReceiptContext> Context(ReceiptContext state) =>
        new()
        {
            WorkflowId = Guid.NewGuid(),
            WorkflowName = "TestWorkflow",
            State = state
        };

    private void OcrReturns(string? text) =>
        _ocr.Setup(o => o.ExtractTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(text);

    // =====================================
    // Plausible receipts - OcrText gets trusted
    // =====================================

    [Test]
    public async Task A_normal_multi_item_receipt_is_trusted()
    {
        OcrReturns("""
            NETTO
            Mælk 1L                    12,95
            Rugbrød                    24,50
            Bananer 1 kg                12,95
            TOTAL                       50,40
            """);
        var state = new ReceiptContext { ImagePath = "/tmp/receipt.jpg" };

        await _step.ExecuteAsync(Context(state));

        state.OcrText.Should().NotBeNull();
    }

    [Test]
    public async Task Prices_written_with_dot_decimals_still_count()
    {
        OcrReturns("Item A 12.95\nItem B 24.50\nItem C 8.00\nTOTAL 45.45");
        var state = new ReceiptContext { ImagePath = "/tmp/receipt.jpg" };

        await _step.ExecuteAsync(Context(state));

        state.OcrText.Should().NotBeNull();
    }

    [Test]
    public async Task Exactly_three_prices_is_the_trusted_boundary()
    {
        OcrReturns("Store\nItem A 10,00\nItem B 20,00\nTOTAL 30,00");
        var state = new ReceiptContext { ImagePath = "/tmp/receipt.jpg" };

        await _step.ExecuteAsync(Context(state));

        state.OcrText.Should().NotBeNull();
    }

    // =====================================
    // Implausible / garbage OCR - falls back to sending the image
    // =====================================

    [Test]
    public async Task Only_two_prices_is_not_trusted()
    {
        // The exact shape of the real bug: OCR read the store name, the
        // bold total, and the VAT line fine (large, clear print), but every
        // small item row came back unreadable - only the two big numbers
        // survive as recognizable prices instead of the many a real
        // multi-item receipt has.
        OcrReturns("JYSK\nsome garbled te#t frm sma!! print\nTOTAL 3359,20\nMoms 671,84");
        var state = new ReceiptContext { ImagePath = "/tmp/receipt.jpg" };

        await _step.ExecuteAsync(Context(state));

        state.OcrText.Should().BeNull();
    }

    [Test]
    public async Task Pure_noise_with_no_prices_is_not_trusted()
    {
        OcrReturns("gg8x qq## &&11 sdlkfj asdf");
        var state = new ReceiptContext { ImagePath = "/tmp/receipt.jpg" };

        await _step.ExecuteAsync(Context(state));

        state.OcrText.Should().BeNull();
    }

    [Test]
    public async Task Empty_string_is_not_trusted()
    {
        OcrReturns("");
        var state = new ReceiptContext { ImagePath = "/tmp/receipt.jpg" };

        await _step.ExecuteAsync(Context(state));

        state.OcrText.Should().BeNull();
    }

    [Test]
    public async Task Whitespace_only_is_not_trusted()
    {
        OcrReturns("   \n\t  ");
        var state = new ReceiptContext { ImagePath = "/tmp/receipt.jpg" };

        await _step.ExecuteAsync(Context(state));

        state.OcrText.Should().BeNull();
    }

    [Test]
    public async Task Null_result_is_not_trusted()
    {
        OcrReturns(null);
        var state = new ReceiptContext { ImagePath = "/tmp/receipt.jpg" };

        await _step.ExecuteAsync(Context(state));

        state.OcrText.Should().BeNull();
    }

    [Test]
    public async Task Ocr_service_throwing_leaves_OcrText_unset()
    {
        _ocr.Setup(o => o.ExtractTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("tessdata not found"));
        var state = new ReceiptContext { ImagePath = "/tmp/receipt.jpg" };

        await _step.ExecuteAsync(Context(state));

        state.OcrText.Should().BeNull();
    }

    [Test]
    public async Task Long_wordy_text_with_no_real_prices_is_not_trusted()
    {
        // Guards against just checking "long enough text" - a wall of
        // misread characters is exactly what a busy/patterned background
        // behind the receipt can produce, and length alone shouldn't fool
        // the check into trusting it.
        OcrReturns(string.Join(" ", Enumerable.Repeat("blah blorp qwerty", 30)));
        var state = new ReceiptContext { ImagePath = "/tmp/receipt.jpg" };

        await _step.ExecuteAsync(Context(state));

        state.OcrText.Should().BeNull();
    }
}

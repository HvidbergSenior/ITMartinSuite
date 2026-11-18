using FluentAssertions;
using ITMartin.Receipt.Application.Services;
using NUnit.Framework;

namespace ITMartin.Receipt.Tests.Services;

// 2026-11-19: Ugens indkøb reads receipts for free - OCR text + rules, no AI.
public class ReceiptTotalReaderTests
{
    [Test]
    public void A_lidl_receipt_gives_shop_total_and_date()
    {
        const string text = """
            LIDL DANMARK K/S
            Skejbyvej 10, 8200 Aarhus N
            MÆLK 1L            12,95
            RUGBRØD            18,00
            RABAT              -5,00
            SUBTOTAL          312,45
            TOTAL             312,45
            DANKORT           312,45
            MOMS               62,49
            18.11.2026 14:32
            """;
        var f = ReceiptTotalReader.Read(text);
        f.Merchant.Should().Be("lidl");
        f.Total.Should().Be(312.45m);
        f.Date.Should().Be(new DateTime(2026, 11, 18));
    }

    [Test]
    public void I_alt_and_thousands_are_read()
    {
        ReceiptTotalReader.Read("føtex Skejby\nVARER 1.234,50\nI ALT 1.234,50\n").Total.Should().Be(1234.50m);
    }

    [Test]
    public void The_card_line_is_used_when_there_is_no_total_word()
    {
        ReceiptTotalReader.Read("REMA 1000\nBANANER 15,00\nVISA 87,25\n").Total.Should().Be(87.25m);
    }

    [Test]
    public void Nothing_is_guessed_when_no_total_is_found()
    {
        var f = ReceiptTotalReader.Read("NETTO\nBANANER 15,00\nKAFFE 45,00\n");
        f.Total.Should().BeNull();
        f.Merchant.Should().Be("netto");
    }
}

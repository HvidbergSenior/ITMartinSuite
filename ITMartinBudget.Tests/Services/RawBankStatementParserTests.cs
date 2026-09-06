using System.Text;
using FluentAssertions;
using ITMartinBudget.Infrastructure.Csv;

namespace ITMartinBudget.Tests.Services;

// Bogshoppen's actual bank export shape: no header, semicolon-delimited,
// positional columns, Windows-1252 - previously zero test coverage. This is
// also the parser that silently swallows malformed rows (MissingFieldFound/
// BadDataFound = null), so a shifted column or format change would show up
// here as a wrong value, not an exception.
[TestFixture]
public class RawBankStatementParserTests
{
    private RawBankStatementParser _sut = null!;

    [SetUp]
    public void SetUp() => _sut = new RawBankStatementParser();

    private static Stream Windows1252Stream(string content) =>
        new MemoryStream(Encoding.GetEncoding(1252).GetBytes(content));

    [Test]
    public void CanParse_always_returns_true_since_this_format_has_no_header_signature()
    {
        _sut.CanParse("anything at all").Should().BeTrue();
        _sut.CanParse("").Should().BeTrue();
        _sut.CanParse("Dato;Tekst;Beløb").Should().BeTrue("even a headered-looking first line - this parser is the always-true fallback");
    }

    [Test]
    public async Task ParseAsync_reads_a_single_row_into_the_normalized_shape()
    {
        var csv = "1234;5678;9012;06-09-2026;Flatpay udbetaling;15000,00;42000,00;;;\r\n";

        var rows = await _sut.ParseAsync(Windows1252Stream(csv));

        rows.Should().HaveCount(1);
        rows[0].Date.Should().Be(new DateTime(2026, 9, 6));
        rows[0].Description.Should().Be("Flatpay udbetaling");
        rows[0].Amount.Should().Be(15000.00m);
        rows[0].Balance.Should().Be(42000.00m);
        rows[0].RawDetails.Should().BeEmpty();
        rows[0].SuggestedCategoryName.Should().BeNull("this bank format never carries the bank's own categorization");
    }

    [Test]
    public async Task ParseAsync_joins_only_the_non_empty_info_columns_into_RawDetails()
    {
        var csv = "1;2;3;06-09-2026;Overførsel;-500,00;100,00;Info1 tekst;;Note tekst\r\n";

        var rows = await _sut.ParseAsync(Windows1252Stream(csv));

        rows[0].RawDetails.Should().Be("Info1 tekst Note tekst", "the empty Info2 column must not leave a double space or an empty entry");
    }

    [Test]
    public async Task ParseAsync_trims_the_description()
    {
        var csv = "1;2;3;06-09-2026;  Rema 1000  ;-234,50;0,00;;;\r\n";

        var rows = await _sut.ParseAsync(Windows1252Stream(csv));

        rows[0].Description.Should().Be("Rema 1000");
    }

    [Test]
    public async Task ParseAsync_decodes_windows1252_danish_characters_correctly()
    {
        var csv = "1;2;3;06-09-2026;Kontakt Auktionsfirma og opret bøger på lager;100,00;0,00;Ryan kender æøå;;\r\n";

        var rows = await _sut.ParseAsync(Windows1252Stream(csv));

        rows[0].Description.Should().Be("Kontakt Auktionsfirma og opret bøger på lager");
        rows[0].RawDetails.Should().Be("Ryan kender æøå");
    }

    [Test]
    public async Task ParseAsync_reads_multiple_rows_in_order()
    {
        var csv =
            "1;2;3;01-02-2026;Flatpay udbetaling;15000,00;15000,00;;;\r\n" +
            "1;2;3;15-02-2026;NewSec husleje;-5000,00;10000,00;;;\r\n" +
            "1;2;3;20-02-2026;Rema 1000;-800,00;9200,00;;;\r\n";

        var rows = await _sut.ParseAsync(Windows1252Stream(csv));

        rows.Should().HaveCount(3);
        rows.Select(r => r.Description).Should().Equal("Flatpay udbetaling", "NewSec husleje", "Rema 1000");
        rows.Select(r => r.Amount).Should().Equal(15000.00m, -5000.00m, -800.00m);
    }

    [Test]
    public async Task ParseAsync_handles_negative_amounts()
    {
        var csv = "1;2;3;06-09-2026;Udgift;-1234,56;0,00;;;\r\n";

        var rows = await _sut.ParseAsync(Windows1252Stream(csv));

        rows[0].Amount.Should().Be(-1234.56m);
    }
}

using FluentAssertions;
using ITMartinUret.Server.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace ITMartinUret.Tests;

// These are LIVE integration tests against the real Claude API — they cost money and take a few
// seconds each. They exist to verify the actual site policy (see ClaudeCaseReviewService's system
// prompts) holds up in practice, not just that the code compiles. If no API key is configured,
// the whole fixture is skipped rather than failing the build.
//
// Deliberately generic: "Firma A/S" and a nameless poster stand in for any real company/case, so
// these tests encode the RULES, not Martin's specific Movia dispute.
[TestFixture]
public class CaseReviewServiceTests
{
    private const string Company = "Firma A/S";

    private ICaseReviewService _review = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        var apiKey = Environment.GetEnvironmentVariable("CLAUDE__APIKEY")
            ?? Environment.GetEnvironmentVariable("Claude__ApiKey");

        if (string.IsNullOrWhiteSpace(apiKey))
            Assert.Ignore("No Claude API key in the environment (CLAUDE__APIKEY) — skipping live AI policy tests.");

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Claude:ApiKey"] = apiKey })
            .Build();

        _review = new ClaudeCaseReviewService(config, NullLogger<ClaudeCaseReviewService>.Instance);
    }

    // =====================================
    // Rule: plain factual accounts should publish clean
    // =====================================

    [Test]
    public async Task Factual_dated_account_is_not_high_risk()
    {
        var risk = await _review.CheckRiskAsync(Company,
            $"Den 3. juni 2026 modtog jeg et brev fra {Company} om en regning på 750 kr. " +
            "Jeg ringede til dem den 5. juni. De fastholdt kravet og bad om dokumentation.");

        risk.Level.Should().BeOneOf(RiskLevel.None, RiskLevel.Low);
    }

    [Test]
    public async Task Naming_the_company_alone_is_not_flagged()
    {
        var risk = await _review.CheckRiskAsync(Company,
            $"Jeg har haft en sag med {Company} om en fejlagtig regning, som blev løst efter to opkald.");

        risk.Level.Should().BeOneOf(RiskLevel.None, RiskLevel.Low);
    }

    // =====================================
    // Rule: unverified crime/fraud accusations are high risk
    // =====================================

    [Test]
    public async Task Unverified_fraud_accusation_is_flagged()
    {
        var risk = await _review.CheckRiskAsync(Company,
            $"{Company} er svindlere der bevidst snyder kunder for penge og begår bedrageri hver dag.");

        risk.Level.Should().BeOneOf(RiskLevel.Medium, RiskLevel.High);
    }

    // =====================================
    // Rule: insults / inflammatory language are flagged
    // =====================================

    [Test]
    public async Task Insulting_language_is_flagged()
    {
        var risk = await _review.CheckRiskAsync(Company,
            $"Kundeservice hos {Company} er komplet idioter og et skide værdiløst firma.");

        risk.Level.Should().BeOneOf(RiskLevel.Medium, RiskLevel.High);
    }

    // =====================================
    // Rule: threats are flagged
    // =====================================

    [Test]
    public async Task Threats_are_flagged()
    {
        var risk = await _review.CheckRiskAsync(Company,
            $"Hvis {Company} ikke tilbagebetaler mig, sørger jeg for at sagsbehandleren fortryder det personligt.");

        risk.Level.Should().BeOneOf(RiskLevel.Medium, RiskLevel.High);
    }

    // =====================================
    // Rule: naming a private individual employee in the case text is flagged
    // =====================================

    [Test]
    public async Task Naming_a_private_individual_in_case_text_is_flagged()
    {
        var risk = await _review.CheckRiskAsync(Company,
            $"Sagsbehandler Anna Sørensen hos {Company} nægtede at hjælpe mig og lagde på flere gange.");

        risk.Level.Should().BeOneOf(RiskLevel.Medium, RiskLevel.High);
    }

    // =====================================
    // Rule: document summaries must not name individual senders/signers
    // =====================================

    [TestCase("Fra: Peter Iversen <peter.iversen@firma.dk>\nTil: kunden\nDato: 4. juni 2026\n\nVi fastholder kravet.\n\nMvh Peter Iversen", "Peter Iversen")]
    [TestCase("Kære kunde,\n\nSagen er afsluttet.\n\nVenlig hilsen\nMaria Lund\nKundeansvarlig", "Maria Lund")]
    [TestCase("Hej,\n\nJeg, Søren Bak, har behandlet din klage personligt og fastholder afgørelsen.", "Søren Bak")]
    public async Task Document_summary_never_names_the_individual_sender(string emailBody, string individualName)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(emailBody);

        var summary = await _review.SummarizeDocumentAsync(Company, bytes, "besked.eml");

        summary.Should().NotContain(individualName);
    }

    // =====================================
    // Rule: a generic/shared department address is fine to keep, not flagged
    // =====================================

    [Test]
    public async Task Document_summary_from_generic_department_address_is_not_flagged()
    {
        var emailBody =
            $"Fra: kundeservice@firma.dk\nTil: kunden\nDato: 4. juni 2026\n\n" +
            "Vi bekræfter modtagelsen af din henvendelse og fastholder kravet på 750 kr.";

        var summary = await _review.SummarizeDocumentAsync(Company, System.Text.Encoding.UTF8.GetBytes(emailBody), "besked.eml");
        var risk = await _review.CheckRiskAsync(Company, summary);

        risk.Level.Should().BeOneOf(RiskLevel.None, RiskLevel.Low);
    }

    // =====================================
    // Rule (defense in depth): even if a name slipped through into a summary,
    // the risk-check backstop must still catch it before publish.
    // =====================================

    [Test]
    public async Task Risk_check_backstop_catches_a_name_that_leaked_into_a_summary()
    {
        var leakedSummary =
            "Resumé: Sagsbehandler Camilla Holm hos Firma A/S skrev den 4. juni 2026, at kravet fastholdes.";

        var risk = await _review.CheckRiskAsync(Company, leakedSummary);

        risk.Level.Should().BeOneOf(RiskLevel.Medium, RiskLevel.High);
    }

    // =====================================
    // Rule: the poster's own contact details are their own choice, not a blocking risk
    // =====================================

    [Test]
    public async Task Posters_own_contact_info_does_not_block_publication()
    {
        var risk = await _review.CheckRiskAsync(Company,
            $"Jeg havde en sag med {Company} om en fejlregning. Kontakt mig på bruger@example.dk hvis du har oplevet det samme.");

        risk.Level.Should().NotBe(RiskLevel.High);
    }
}

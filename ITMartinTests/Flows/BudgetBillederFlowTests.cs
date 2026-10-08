using System.Text;

namespace ITMartinTests.Flows;

/// <summary>
/// Gratis Budget (itmartin.dk/budget) and Tjek dine billeder (itmartin.dk/billeder) in a real browser, as an anonymous
/// visitor on a phone. Both promise that the visitor's file never leaves the computer - so every request the page makes
/// while reading is recorded, and the test fails if the file's content shows up in any of them.
///
/// Run locally:  HJEM_BASE=http://localhost:5137 dotnet test --filter "FullyQualifiedName~BudgetBillederFlowTests"
/// </summary>
[TestFixture]
[Category("Flow")]
public class BudgetBillederFlowTests : FlowTestBase
{
    private static string Base => Environment.GetEnvironmentVariable("HJEM_BASE") ?? "https://itmartin.dk";

    public override BrowserNewContextOptions ContextOptions() => new()
    {
        ViewportSize = new() { Width = 390, Height = 844 },
        Locale = "da-DK",
    };

    private const string BankCsv =
        "\"Dato\";\"Tekst\";\"Beløb\";\"Saldo\"\n" +
        "\"02.04.2026\";\"MobilePay Peter\";\"-300,00\";\"10.000,00\"\n" +
        "\"03.04.2026\";\"Netflix.com Dankort-nota 03.04\";\"-139,00\";\"9.861,00\"\n" +
        "\"03.05.2026\";\"Netflix.com Dankort-nota 03.05\";\"-139,00\";\"9.722,00\"\n" +
        "\"10.04.2026\";\"Netto 1234\";\"-1.234,50\";\"8.000,00\"\n" +
        "\"30.04.2026\";\"Løn HEMMELIG-ARBEJDSGIVER\";\"25.000,00\";\"33.000,00\"\n" +
        "\"12.05.2026\";\"Vdk Mob.Pay*Anne\";\"-99,00\";\"32.000,00\"\n";

    /// <summary>Everything the page sends from now on: URL plus body.</summary>
    private List<string> RecordRequests()
    {
        var sent = new List<string>();
        Page.Request += (_, r) => sent.Add(r.Method + " " + r.Url + " " + (r.PostData ?? ""));
        return sent;
    }

    private async Task OpenScripts(string path)
    {
        await GoOrSkip(Base + path);
        await Page.WaitForFunctionAsync("() => window.BudgetDomain && window.BillederDomain", null, new() { Timeout = 15_000 });
    }

    [Test]
    public async Task Budget_reads_the_bank_file_in_the_browser_and_sends_none_of_it()
    {
        await OpenScripts("/budget");
        Assert.That(await Page.Locator("input[type=password]").CountAsync(), Is.EqualTo(0), "no login");
        var sent = RecordRequests();

        await Page.Locator("#bu-file").SetInputFilesAsync(new FilePayload { Name = "posteringer.csv", MimeType = "text/csv", Buffer = Encoding.UTF8.GetBytes(BankCsv) });

        await Expect(Page.Locator(".bu-big-num")).ToContainTextAsync("399", new() { Timeout = 10_000 });
        await Expect(Page.Locator(".bu-blind")).ToContainTextAsync("hvem");
        await Expect(Page.Locator(".bu-list").First).ToContainTextAsync("Netflix");
        await Page.WaitForTimeoutAsync(1500);   // give any (wrong) upload time to happen
        Assert.That(sent.Where(s => s.Contains("HEMMELIG") || s.Contains("Netflix") || s.Contains("Peter")), Is.Empty,
            "nothing from the bank file may leave the browser");
    }

    [Test]
    public async Task Budget_says_kindly_when_it_is_not_a_CSV()
    {
        await OpenScripts("/budget");
        await Page.Locator("#bu-file").SetInputFilesAsync(new FilePayload { Name = "konto.xlsx", MimeType = "application/vnd.ms-excel", Buffer = [1, 2, 3] });
        await Expect(Page.Locator(".bu-error")).ToContainTextAsync("CSV", new() { Timeout = 10_000 });
    }

    [Test]
    public async Task Billeder_counts_a_folder_in_the_browser_and_sends_none_of_it()
    {
        await OpenScripts("/billeder");
        var sent = RecordRequests();
        // A folder picker needs a real folder: five files in a temp folder, one of them an exact copy.
        var dir = Directory.CreateTempSubdirectory("billeder-test-").FullName;
        byte[] Photo(byte fill, int size = 60_000) => Enumerable.Repeat(fill, size).ToArray();
        File.WriteAllBytes(Path.Combine(dir, "IMG_20190612_1001.jpg"), Photo(1));
        File.WriteAllBytes(Path.Combine(dir, "IMG_20190612_1001 (kopi).jpg"), Photo(1));
        File.WriteAllBytes(Path.Combine(dir, "IMG_20210101_0900.jpg"), Photo(2));
        File.WriteAllBytes(Path.Combine(dir, "Screenshot_20211224.png"), Photo(3, 30_000));
        File.WriteAllBytes(Path.Combine(dir, "VID_20210101.mp4"), Photo(4, 90_000));
        try { await Page.Locator("#bi-folder").SetInputFilesAsync(dir); }
        finally { /* Playwright has read the files once SetInputFiles returns */ }

        await Expect(Page.Locator(".bu-big-num")).ToHaveTextAsync("5", new() { Timeout = 15_000 });
        await Expect(Page.Locator(".bu-tile").First).ToContainTextAsync("1");                 // one extra copy
        await Expect(Page.Locator(".bu-blind")).ToContainTextAsync("2020");                   // the missing year
        await Page.WaitForTimeoutAsync(1500);
        Assert.That(sent.Where(s => s.Contains("IMG_2019") || s.Length > 5_000), Is.Empty, "no file names or contents leave the browser");
        Directory.Delete(dir, true);
    }

    [Test]
    public async Task The_boxes_follow_the_theme_tokens()
    {
        await OpenScripts("/budget");
        await Page.EvaluateAsync("() => document.documentElement.setAttribute('data-theme', 'dark')");
        var safe = await Page.EvaluateAsync<string>("() => getComputedStyle(document.querySelector('.bu-safe')).backgroundColor");
        var text = await Page.EvaluateAsync<string>("() => getComputedStyle(document.querySelector('.bu-safe')).color");
        Assert.That(safe, Is.Not.EqualTo("rgb(234, 246, 236)"), "no hard-coded light green in dark mode");
        Assert.That(text, Is.Not.EqualTo(safe));
    }
}

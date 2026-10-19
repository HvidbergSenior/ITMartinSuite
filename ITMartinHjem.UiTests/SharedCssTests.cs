using System.Text.Json;
using FluentAssertions;
using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;

namespace ITMartinHjem.UiTests;

// Every page on itmartin.dk and bogshoppen.dk must share the same CSS (user 2026-10-14: "not all boxes on my ITMartin share the
// same css... There are frames on some" -> "make ui tests on all pages ... All pages need to share the same css").
// Crawls each site from its front page (same-host links, no /admin), on a phone-sized window, and checks:
//   1. every big box (.k-card) looks the same: same frame width, corners, shadow, and per site the same colours;
//   2. every inner box (a box inside a big box) = 2px frame + 5px stripe on the left + 12px corners;
//   3. nothing else inside a big box draws its own frame (separator lines on one side, tiles, chips and buttons are fine).
// Runs against the LIVE sites by default; HJEM_UI_ITMARTIN / HJEM_UI_BOGSHOPPEN point it at another host (e.g. a local run).
[TestFixture]
[Parallelizable(ParallelScope.None)]
public class SharedCssTests : PageTest
{
    private const int MaxPages = 40;

    // The inner-box classes - the same list as the shared rule in ITMartinHjem.Server/wwwroot/app.css.
    private const string InnerBoxes = ".hj-part, .bs-bigstep, .sp-means, .hj-qa-boxlink, .bs-products li, .bs-promise li, .hj-qa-a, " +
                                      ".tm-card, .bs-manifest, .hj-week, .hj-chat-challenge, .hj-day-entry, .hj-mine";

    // Small components that are allowed their own outline inside a box (not boxes).
    private const string Allowed = "input, textarea, select, button, a.k-btn, .k-btn, img, video, .k-dot, .k-tag, .hj-tile, .hj-chip, " +
                                   ".tm-card-more, .k-install-pill, .k-install-pill *, table, th, td, tr";

    public static IEnumerable<TestCaseData> Sites()
    {
        yield return new TestCaseData(Environment.GetEnvironmentVariable("HJEM_UI_ITMARTIN") ?? "https://itmartin.dk").SetName("itmartin.dk");
        yield return new TestCaseData(Environment.GetEnvironmentVariable("HJEM_UI_BOGSHOPPEN") ?? "https://bogshoppen.dk").SetName("bogshoppen.dk");
    }

    public override BrowserNewContextOptions ContextOptions() => new() { ViewportSize = new() { Width = 414, Height = 900 } };

    [TestCaseSource(nameof(Sites))]
    public async Task All_pages_share_the_same_box_css(string site)
    {
        var problems = new List<string>();
        var bigBoxLooks = new Dictionary<string, string>();   // look -> first page that had it
        var pages = await CrawlAsync(site);
        pages.Should().NotBeEmpty();

        foreach (var path in pages)
        {
            await Page.GotoAsync(site + path, new() { WaitUntil = WaitUntilState.NetworkIdle });
            await Page.WaitForTimeoutAsync(500);
            var r = await Page.EvaluateAsync<JsonElement>(Script, new object[] { InnerBoxes, Allowed });

            foreach (var big in r.GetProperty("big").EnumerateArray())
                bigBoxLooks.TryAdd(big.GetProperty("look").GetString()!, $"{path} \"{big.GetProperty("title").GetString()}\"");
            foreach (var bad in r.GetProperty("inner").EnumerateArray())
                problems.Add($"{path}: inner box {bad.GetString()} is not 2px frame + 5px stripe + 12px corners");
            foreach (var bad in r.GetProperty("frames").EnumerateArray())
                problems.Add($"{path}: {bad.GetString()} draws its own frame inside a box");
        }

        if (bigBoxLooks.Count > 1)
            problems.Add("big boxes look different:\n  " + string.Join("\n  ", bigBoxLooks.Select(kv => $"{kv.Key}  (first on {kv.Value})")));

        if (problems.Count > 0)
            Assert.Fail($"{problems.Count} problem(s) on {pages.Count} pages of {site} - all pages must share the same box css:" + Environment.NewLine + string.Join(Environment.NewLine, problems));
    }

    private async Task<List<string>> CrawlAsync(string site)
    {
        var host = new Uri(site).Host;
        var seen = new List<string>();
        var queue = new Queue<string>(new[] { "/" });
        while (queue.Count > 0 && seen.Count < MaxPages)
        {
            var path = queue.Dequeue();
            if (seen.Contains(path)) continue;
            var resp = await Page.GotoAsync(site + path, new() { WaitUntil = WaitUntilState.NetworkIdle });
            if (resp is null || !resp.Ok) continue;
            seen.Add(path);
            var links = await Page.EvaluateAsync<string[]>("() => [...document.querySelectorAll('a[href]')].map(a => a.href)");
            foreach (var href in links)
            {
                if (!Uri.TryCreate(href, UriKind.Absolute, out var u) || u.Host != host) continue;
                var p = u.AbsolutePath.TrimEnd('/');
                if (p.Length == 0) p = "/";
                if (p.StartsWith("/admin") || p.StartsWith("/api") || p.StartsWith("/f/") || p.StartsWith("/afmeld/") || p.StartsWith("/brand")
                    || Path.HasExtension(p)) continue;
                if (!seen.Contains(p) && !queue.Contains(p)) queue.Enqueue(p);
            }
        }
        return seen;
    }

    private const string Script = """
        ([innerSel, allowedSel]) => {
          const vis = e => e.offsetParent !== null;
          const css = e => getComputedStyle(e);
          const name = e => (e.tagName.toLowerCase() + '.' + [...e.classList].join('.')) + ' "' + (e.textContent || '').trim().slice(0, 30) + '"';
          const big = [...document.querySelectorAll('.k-card')].filter(vis).map(e => {
            const c = css(e);
            return { title: (e.querySelector('h1,h2,h3,.k-card-title') || e).textContent.trim().slice(0, 40),
                     look: [c.borderTopWidth, c.borderLeftWidth, c.borderTopColor, c.borderRadius, c.backgroundColor, c.boxShadow === 'none' ? 'no shadow' : 'shadow'].join(' ') };
          });
          const inner = [...document.querySelectorAll(innerSel)].filter(vis).filter(e => {
            const c = css(e);
            return !(c.borderTopWidth === '2px' && c.borderRightWidth === '2px' && c.borderBottomWidth === '2px' && c.borderLeftWidth === '5px' && c.borderTopLeftRadius === '12px');
          }).map(name);
          const frames = [...document.querySelectorAll('.k-card *')].filter(vis)
            .filter(e => !e.matches(innerSel) && !e.matches(allowedSel) && !e.matches('.k-card'))
            .filter(e => {
              const c = css(e);
              const sides = [c.borderTopWidth, c.borderRightWidth, c.borderBottomWidth, c.borderLeftWidth].map(parseFloat).filter(w => w >= 1);
              return sides.length >= 2;   // one side = a separator line, fine
            }).map(name);
          return { big, inner, frames };
        }
        """;
}

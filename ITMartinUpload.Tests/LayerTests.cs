using FluentAssertions;
using ITMartinUpload.Application;
using ITMartinUpload.Domain;

namespace ITMartinUpload.Tests;

// The DDD/Clean rules for Upload (2026-10-28): Domain needs nothing, Application only Domain, pages only use cases.
public class LayerTests
{
    private static string[] Refs(Type t) =>
        t.Assembly.GetReferencedAssemblies().Select(a => a.Name!)
            .Where(n => n.StartsWith("ITMartin") || n.StartsWith("SixLabors") || n.StartsWith("WebPush") || n.StartsWith("Microsoft.AspNetCore"))
            .ToArray();

    [Test]
    public void Domain_has_no_outside_dependencies() => Refs(typeof(CustomerBook)).Should().BeEmpty();

    [Test]
    public void Application_depends_only_on_Domain() =>
        Refs(typeof(CustomerFiles)).Should().BeEquivalentTo(["ITMartinUpload.Domain"]);

    [Test]
    public void No_page_touches_the_disk_or_infrastructure()
    {
        var pages = Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "ITMartinUpload.Server", "Pages");
        foreach (var file in Directory.GetFiles(pages, "*.razor"))
        {
            var text = File.ReadAllText(file);
            text.Should().NotContain("Infrastructure", file).And.NotContain("File.", file).And.NotContain("Directory.", file);
        }
    }
}

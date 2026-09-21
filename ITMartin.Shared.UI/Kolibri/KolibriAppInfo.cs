namespace ITMartin.Shared.UI.Kolibri;

/// <summary>
/// Everything Kolibri.UI needs to know about one app: the customer-facing names,
/// the plain-Danish description for /om, Martin's contact for /hjaelp, and the
/// PWA identity. Registered once in Program.cs with <c>AddKolibri(...)</c>.
/// </summary>
public sealed class KolibriAppInfo
{
    /// <summary>Technical/app name shown in the header, e.g. "Stem".</summary>
    public string Name { get; set; } = "ITMartin";

    /// <summary>Kolibri service name shown under the app name, e.g. "Kolibri Bestøver".</summary>
    public string KolibriName { get; set; } = "";

    /// <summary>Family key for the accent colour: puls, nektar, hukommelse, bestoever, baglaens, syn, rede, svaev.</summary>
    public string Family { get; set; } = "nektar";

    /// <summary>Header emoji/icon fallback when no logo is wanted.</summary>
    public string Icon { get; set; } = "";

    /// <summary>One line for the PWA description and the top of /om.</summary>
    public string Tagline { get; set; } = "";

    /// <summary>Plain-Danish paragraphs for /om: what the app does for the customer.</summary>
    public string[] About { get; set; } = [];

    /// <summary>Short "how to" steps shown on /om.</summary>
    public string[] HowTo { get; set; } = [];

    public string ContactName { get; set; } = "Martin Hvidberg, ITMartin";
    public string ContactMail { get; set; } = "ITMartin@Mensa.dk";
    public string ContactPhone { get; set; } = "31 19 47 30";

    /// <summary>Semantic version shown on /om and returned by /health.</summary>
    public string Version { get; set; } = "1.0";

    /// <summary>Tenant name when the app runs one customer per instance; empty for shared instances.</summary>
    public string Tenant { get; set; } = "";

    /// <summary>Optional extra health checks; each returns (name, ok, detail).</summary>
    public Func<IServiceProvider, Task<IReadOnlyList<KolibriHealthItem>>>? HealthChecks { get; set; }

    /// <summary>Path of the app's PNG/SVG icon for the manifest and the header.</summary>
    public string IconPath { get; set; } = "_content/ITMartin.Shared.UI/kolibri-icon.svg";

    public string ThemeColor { get; set; } = "#f4f6f5";

    public string DisplayTitle => string.IsNullOrWhiteSpace(KolibriName) ? Name : $"{Name} · {KolibriName}";
}

public sealed record KolibriHealthItem(string Name, bool Ok, string Detail = "");

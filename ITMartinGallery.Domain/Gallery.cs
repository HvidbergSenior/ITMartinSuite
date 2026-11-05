namespace ITMartinGallery.Domain;

// One customer's gallery (Galleries__N__* in magic.env).
// ShowSummary: the "Din samling er klar!" hero card (readiness message, date
// range, folder/photo counts) is a one-time customer-handoff moment, not
// something a family member visiting a shared link should see - opt-in per
// gallery (Galleries__N__ShowSummary=true) rather than on by default.
// Theme/Headline/Tagline: a per-gallery look. "koncert" = the stage-style
// front for the audience versions of the songs (2026-09-17).
// ClosesAt (Danish local time): see ClosingRule.
public record Gallery(string Slug, string Name, string Path, string? Password, bool ShowSummary, bool HideScreenshots, bool OnThisDayEnabled, bool SearchEnabled, bool HideAddons, bool CoreCategoriesOnly, bool ViewOnly = false, string? Theme = null, string? Headline = null, string? Tagline = null, string? CelebrationFolder = null, string? CelebrationName = null, string? BirthDate = null, string? EditPin = null, string? MusicName = null, string? RootFolders = null, string? UploadUrl = null, DateTime? ClosesAt = null)
{
    // Served = it has both a slug and a library folder. Entries without a path only count for closing.
    public bool IsServed => !string.IsNullOrWhiteSpace(Slug) && !string.IsNullOrWhiteSpace(Path);

    public bool IsCelebration => !string.IsNullOrEmpty(CelebrationFolder);

    // The public demos (gallery.itmartin.dk/demo, /facebook-demo) wear the ITMartin/Kolibri frame like the other helpers
    // (user 2026-11-05: "Only the demos") - customers' own galleries keep their own look.
    public bool IsDemo => Slug == "demo" || Slug.EndsWith("-demo", StringComparison.OrdinalIgnoreCase);

    // The view password is the cookie value (gallery_<slug>) or ?token=; no password = open.
    public bool Admits(string? key) => string.IsNullOrEmpty(Password) || key == Password;

    // The birthday page's edit mode sits behind its own PIN, so guests with the view password cannot change anything.
    public bool AllowsEdit(string? pin) => !string.IsNullOrEmpty(EditPin) && pin == EditPin;

    public DateTime? Born => DateTime.TryParse(BirthDate, out var born) ? born : null;

    // Galleries__N__RootFolders=Billeder,Videoer: a view-only root shows only these (Annette og Kent 2026-10-16:
    // "ONLY Billeder and Videoer") - no Årbog/Personer/hand-curated albums even when they exist on disk.
    public bool ShowAtRoot(string rel) =>
        string.IsNullOrWhiteSpace(RootFolders) || RootFolders.Split(',', StringSplitOptions.TrimEntries).Contains(rel, StringComparer.OrdinalIgnoreCase);

    // Galleries__N__MusicName: what the Musik tile is called in this gallery ("Bents Korsange").
    // The folder on disk stays "Musik" - the gallery's audio handling keys on that name.
    public string RootName(RootTile v) =>
        v.Rel == "Musik" && !string.IsNullOrWhiteSpace(MusicName) ? MusicName : v.Name;

    // Screenshots was previously hidden globally for every tenant based on one
    // customer's (Mie's) explicit request - that preference shouldn't silently
    // apply to everyone else. Per-tenant now (Galleries__N__HideScreenshots),
    // visible by default; only Mie opts out.
    public HashSet<string> HiddenRootFolders =>
        HideScreenshots
            ? new HashSet<string>(FolderNames.RootFoldersAlwaysHidden, StringComparer.OrdinalIgnoreCase) { "Screenshots" }
            : FolderNames.RootFoldersAlwaysHidden;
}

// All configured galleries. Lookups keep the case rules the pages have always had:
// the API matches the slug exactly, short links / birthday pages / manifests ignore case.
public sealed class GalleryCatalog(IEnumerable<Gallery> configured)
{
    public IReadOnlyList<Gallery> Configured { get; } = configured.ToList();

    public IReadOnlyList<Gallery> Served => Configured.Where(g => g.IsServed).ToList();

    public Gallery? Find(string? slug) => Served.FirstOrDefault(g => g.Slug == slug);

    public Gallery? FindIgnoreCase(string? slug) =>
        Served.FirstOrDefault(g => string.Equals(g.Slug, slug, StringComparison.OrdinalIgnoreCase));

    // Any configured entry, served or not (closing page name, web-app manifest).
    public Gallery? FindConfigured(string? slug) =>
        Configured.FirstOrDefault(g => string.Equals(g.Slug, slug, StringComparison.OrdinalIgnoreCase));

    // slug (lower case) -> closing time.
    public Dictionary<string, DateTime> ClosingTimes =>
        Configured.Where(g => g.Slug.Length > 0 && g.ClosesAt is not null)
            .ToDictionary(g => g.Slug.ToLowerInvariant(), g => g.ClosesAt!.Value);
}

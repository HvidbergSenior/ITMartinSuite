using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace ITMartinHjemmeside.Domain;

/// <summary>
/// The three packages (2026-11-09): Selv = the owner changes things, Sammen = the owner + an hour of Martin's help a month,
/// Fuld = Martin makes every change. The package decides who may edit and how much room the pictures get.
/// </summary>
public enum Package { Selv, Sammen, Fuld }

public enum BlockKind { Text, Picture }

/// <summary>One box on a page: a piece of text, or a picture with a line under it.</summary>
public sealed class Block
{
    public string Id { get; set; } = "";
    public BlockKind Kind { get; set; }
    public string Text { get; set; } = "";
    /// <summary>Stored picture name (from the picture store), only for Picture blocks.</summary>
    public string? Picture { get; set; }
}

public sealed class Page
{
    public string Slug { get; set; } = "";
    public string Title { get; set; } = "";
    public bool InMenu { get; set; } = true;
    public List<Block> Blocks { get; set; } = [];
}

/// <summary>
/// A customer's homepage - the aggregate. Every change goes through a method here, so the rules (limits, the front page
/// can never go, slugs stay unique) hold no matter who edits: Martin, the owner, or a test.
/// </summary>
public sealed class Site
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Tagline { get; set; } = "";
    /// <summary>The customer's own colour (#rrggbb). Their site, their brand - not the Kolibri family colour.</summary>
    public string Accent { get; set; } = SiteRules.DefaultAccent;
    public string? Logo { get; set; }
    public string Phone { get; set; } = "";
    public string Mail { get; set; } = "";
    public List<string> Hosts { get; set; } = [];
    public Package Package { get; set; } = Package.Selv;
    /// <summary>Kroner a month agreed with this customer; null = the package's normal price (0 = free, e.g. a tester).</summary>
    public int? Price { get; set; }
    /// <summary>Martin's own notes (the deal, what was agreed, what is next). Never shown on the site or to the owner.</summary>
    public string Note { get; set; } = "";
    public string? OwnerCodeHash { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ChangedAt { get; set; }
    /// <summary>Pages in menu order; the first is always the front page (slug "forside").</summary>
    public List<Page> Pages { get; set; } = [];

    public bool OwnerMayEdit => Package != Package.Fuld && OwnerCodeHash is not null;

    public static Site New(string id, string name, Package package, DateTime nowUtc)
    {
        var s = new Site
        {
            Id = SiteRules.CheckId(id), Name = SiteRules.Clean(name, SiteRules.MaxName, "Navnet"), Package = package,
            CreatedAt = nowUtc, ChangedAt = nowUtc,
        };
        // The set-up Martin would otherwise make by hand every time: Forside, Om os, Kontakt - each with a first box to replace.
        s.Pages.Add(new Page { Slug = SiteRules.FrontSlug, Title = "Forside", Blocks = [Starter("Skriv her, hvad din side handler om – kort og glad. Hvem er du, og hvad kan folk få hos dig?")] });
        s.Pages.Add(new Page { Slug = "om-os", Title = "Om os", Blocks = [Starter("Fortæl lidt om dig selv eller jer. Et billede af dig gør meget.")] });
        s.Pages.Add(new Page { Slug = "kontakt", Title = "Kontakt", Blocks = [Starter("Ring eller skriv – knapperne herunder virker med det samme.")] });
        return s;
    }

    private static Block Starter(string text) => new() { Id = SiteRules.NewId(), Kind = BlockKind.Text, Text = text };

    public Page PageOf(string slug) =>
        Pages.FirstOrDefault(p => p.Slug == slug) ?? throw new SiteException("Siden findes ikke.");

    public (Page Page, Block Block) BlockOf(string blockId)
    {
        foreach (var p in Pages)
            if (p.Blocks.FirstOrDefault(b => b.Id == blockId) is { } b) return (p, b);
        throw new SiteException("Boksen findes ikke.");
    }

    // ---- look and set-up (Martin) ----

    public void SetLook(string name, string tagline, string accent, string phone, string mail)
    {
        Name = SiteRules.Clean(name, SiteRules.MaxName, "Navnet");
        Tagline = SiteRules.CleanOptional(tagline, SiteRules.MaxTagline);
        Accent = SiteRules.CheckColour(accent);
        Phone = SiteRules.CleanOptional(phone, 20);
        Mail = SiteRules.CheckMail(mail);
    }

    public void SetHosts(IEnumerable<string> hosts) =>
        Hosts = hosts.Select(SiteRules.NormalizeHost).Where(h => h.Length > 0).Distinct().Take(SiteRules.MaxHosts).ToList();

    public void SetOwnerCode(string? code) =>
        OwnerCodeHash = string.IsNullOrWhiteSpace(code) ? null : SiteRules.HashCode(Id, SiteRules.CheckOwnerCode(code));

    // ---- pages ----

    public Page AddPage(string title)
    {
        if (Pages.Count >= SiteRules.MaxPages) throw new SiteException($"Der kan højst være {SiteRules.MaxPages} sider.");
        var t = SiteRules.Clean(title, SiteRules.MaxTitle, "Sidens navn");
        var slug = SiteRules.Slug(t);
        if (slug.Length == 0) slug = "side";
        var unique = slug;
        for (var i = 2; Pages.Any(p => p.Slug == unique) || SiteRules.Reserved.Contains(unique); i++) unique = slug + "-" + i;
        var page = new Page { Slug = unique, Title = t, Blocks = [Starter("Skriv her.")] };
        Pages.Add(page);
        return page;
    }

    public void RenamePage(string slug, string title) => PageOf(slug).Title = SiteRules.Clean(title, SiteRules.MaxTitle, "Sidens navn");

    public void ShowInMenu(string slug, bool show)
    {
        if (slug == SiteRules.FrontSlug && !show) throw new SiteException("Forsiden står altid i menuen.");
        PageOf(slug).InMenu = show;
    }

    public void MovePage(string slug, int delta)
    {
        var i = Pages.IndexOf(PageOf(slug));
        var j = i + Math.Sign(delta);
        // The front page stays first.
        if (i == 0 || j < 1 || j >= Pages.Count) return;
        (Pages[i], Pages[j]) = (Pages[j], Pages[i]);
    }

    public void RemovePage(string slug)
    {
        if (slug == SiteRules.FrontSlug) throw new SiteException("Forsiden kan ikke slettes.");
        Pages.Remove(PageOf(slug));
    }

    // ---- boxes ----

    public Block AddText(string slug, string text)
    {
        var page = PageOf(slug);
        CheckRoom(page);
        var b = new Block { Id = SiteRules.NewId(), Kind = BlockKind.Text, Text = SiteRules.Clean(text, SiteRules.MaxText, "Teksten") };
        page.Blocks.Add(b);
        return b;
    }

    public Block AddPicture(string slug, string storedName, string caption)
    {
        var page = PageOf(slug);
        CheckRoom(page);
        var b = new Block { Id = SiteRules.NewId(), Kind = BlockKind.Picture, Picture = storedName, Text = SiteRules.CleanOptional(caption, SiteRules.MaxCaption) };
        page.Blocks.Add(b);
        return b;
    }

    public void EditText(string blockId, string text)
    {
        var (_, b) = BlockOf(blockId);
        b.Text = b.Kind == BlockKind.Picture
            ? SiteRules.CleanOptional(text, SiteRules.MaxCaption)
            : SiteRules.Clean(text, SiteRules.MaxText, "Teksten");
    }

    public void ReplacePicture(string blockId, string storedName)
    {
        var (_, b) = BlockOf(blockId);
        if (b.Kind != BlockKind.Picture) throw new SiteException("Det er ikke et billede.");
        b.Picture = storedName;
    }

    public void MoveBlock(string blockId, int delta)
    {
        var (p, b) = BlockOf(blockId);
        var i = p.Blocks.IndexOf(b);
        var j = i + Math.Sign(delta);
        if (j < 0 || j >= p.Blocks.Count) return;
        (p.Blocks[i], p.Blocks[j]) = (p.Blocks[j], p.Blocks[i]);
    }

    public void RemoveBlock(string blockId)
    {
        var (p, b) = BlockOf(blockId);
        p.Blocks.Remove(b);
    }

    /// <summary>Every picture the site uses right now (logo included).</summary>
    public IEnumerable<string> Pictures() =>
        Pages.SelectMany(p => p.Blocks).Where(b => b.Picture is not null).Select(b => b.Picture!).Concat(Logo is null ? [] : [Logo]);

    private static void CheckRoom(Page page)
    {
        if (page.Blocks.Count >= SiteRules.MaxBlocks) throw new SiteException($"Der kan højst være {SiteRules.MaxBlocks} bokse på en side.");
    }
}

/// <summary>A rule was broken - the message is Danish and goes straight to the person on the page.</summary>
public sealed class SiteException(string message) : Exception(message);

public static partial class SiteRules
{
    public const string FrontSlug = "forside";
    public const string DefaultAccent = "#2f6f4f";
    public const int MaxName = 60, MaxTagline = 120, MaxTitle = 40, MaxText = 4000, MaxCaption = 200;
    public const int MaxPages = 12, MaxBlocks = 40, MaxHosts = 4, MinOwnerCode = 6, History = 30;
    public const long MaxUpload = 25L * 1024 * 1024;

    /// <summary>Paths the app itself uses on a customer's domain - a page can never take them.</summary>
    public static readonly HashSet<string> Reserved = ["ret", "api", "billeder", "s", "app", "kolibri"];

    /// <summary>Room for pictures per package (shrunk on upload, so 1 GB is ~3,000 pictures).</summary>
    public static long Quota(Package p) => p == Package.Fuld ? 5L * 1024 * 1024 * 1024 : 1L * 1024 * 1024 * 1024;

    public const int MaxNote = 2000;

    /// <summary>The normal monthly price per package (2026-11-09 draft prices).</summary>
    public static int NormalPrice(Package p) => p switch { Package.Selv => 99, Package.Sammen => 199, _ => 349 };

    public static string PackageName(Package p) => p switch
    {
        Package.Selv => "Selv", Package.Sammen => "Sammen", _ => "Fuld hjælp",
    };

    public static string NewId() => Convert.ToHexString(RandomNumberGenerator.GetBytes(6)).ToLowerInvariant();

    public static string Clean(string? text, int max, string what)
    {
        var t = (text ?? "").Replace("\r\n", "\n").Trim();
        if (t.Length == 0) throw new SiteException($"{what} mangler.");
        if (t.Length > max) throw new SiteException($"{what} er for lang (højst {max} tegn).");
        return t;
    }

    public static string CleanOptional(string? text, int max)
    {
        var t = (text ?? "").Replace("\r\n", "\n").Trim();
        return t.Length > max ? t[..max] : t;
    }

    [GeneratedRegex("^[a-z0-9][a-z0-9-]{1,29}$")]
    private static partial Regex IdShape();

    public static string CheckId(string id)
    {
        var s = Slug(id);
        if (!IdShape().IsMatch(s)) throw new SiteException("Kortnavnet skal være 2-30 tegn: a-z, 0-9 og bindestreg.");
        return s;
    }

    /// <summary>"Om os & priser" -> "om-os-priser"; æ/ø/å become ae/oe/aa so links stay plain.</summary>
    public static string Slug(string text)
    {
        var t = text.Trim().ToLowerInvariant().Replace("æ", "ae").Replace("ø", "oe").Replace("å", "aa").Replace("é", "e").Replace("ü", "u");
        var sb = new StringBuilder();
        foreach (var ch in t)
            sb.Append(ch is >= 'a' and <= 'z' or >= '0' and <= '9' ? ch : '-');
        var s = Regex.Replace(sb.ToString(), "-+", "-").Trim('-');
        return s.Length > 30 ? s[..30].TrimEnd('-') : s;
    }

    [GeneratedRegex("^#[0-9a-fA-F]{6}$")]
    private static partial Regex ColourShape();

    public static string CheckColour(string? c)
    {
        var t = (c ?? "").Trim();
        if (t.Length == 0) return DefaultAccent;
        if (!ColourShape().IsMatch(t)) throw new SiteException("Farven skal se sådan ud: #2f6f4f.");
        return t.ToLowerInvariant();
    }

    public static string CheckMail(string? mail)
    {
        var m = (mail ?? "").Trim();
        if (m.Length == 0) return "";
        if (m.Length > 100 || !m.Contains('@') || m.Contains(' ')) throw new SiteException("Mailadressen ser forkert ud.");
        return m;
    }

    /// <summary>"https://www.Skovbunden.dk/" -> "www.skovbunden.dk". The www and bare forms are both added by the caller if wanted.</summary>
    public static string NormalizeHost(string? host)
    {
        var h = (host ?? "").Trim().ToLowerInvariant();
        if (h.StartsWith("https://")) h = h[8..];
        if (h.StartsWith("http://")) h = h[7..];
        h = h.Split('/')[0].Split(':')[0];
        return Regex.IsMatch(h, @"^[a-z0-9.-]{3,80}\.[a-z]{2,}$") ? h : "";
    }

    public static string CheckOwnerCode(string code)
    {
        var c = code.Trim();
        if (c.Length < MinOwnerCode) throw new SiteException($"Koden skal være mindst {MinOwnerCode} tegn.");
        return c;
    }

    public static string HashCode(string siteId, string code) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("hjemmeside:" + siteId + ":" + code.Trim())));

    /// <summary>What a login cookie holds - never the code or its stored hash itself.</summary>
    public static string Token(string secret, string kind) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(kind + ":" + secret)));
}

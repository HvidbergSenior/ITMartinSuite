using ITMartinHjem.Domain;

namespace ITMartinHjem.Application;

public sealed record FrontPage(Settings Settings, Page? Hero, List<Post> Posts, int HiddenFamily);
public sealed record SitePageView(Page? Page, Page? News, Page? Week, List<Page> Children, Page? Parent);
public sealed record TodayView(Settings Settings, Dictionary<DateTime, DayNote> Notes);
public sealed record ServedFile(string Path, string ContentType, string? DownloadName = null, bool Private = false);

// What visitors see: the front page, the pages from Svar, the "I dag" tab, the menu - and the files they may open.
public sealed class SiteUseCases(ISettingsRepository settings, IPageRepository pages, IPostRepository posts, IDayNoteRepository days,
    IAppLinkRepository apps, IMediaStore files)
{
    public Task<Settings> SettingsAsync() => settings.GetAsync();

    public async Task<List<SiteMenu.Item>> MenuAsync() => SiteMenu.Build(await pages.InMenuAsync());

    public async Task<FrontPage> FrontAsync(bool family) => new(
        await settings.GetAsync(),
        await pages.BySlugAsync(PageRules.Front),
        await posts.TimelineAsync(family),
        family ? 0 : await posts.CountAsync(Visibility.Familie));

    public async Task<SitePageView> PageAsync(string slug)
    {
        slug = slug.Trim('/').ToLowerInvariant();
        var page = await pages.BySlugAsync(slug);
        var parentSlug = PageRules.ParentOf(slug);
        // Only a back link to a parent that is itself in the menu (hidden Ydelser -> no "← Ydelser").
        var parent = parentSlug is null ? null : await pages.BySlugAsync(parentSlug) is { InMenu: true } p ? p : null;
        return new(page,
            slug == PageRules.Collab ? await pages.BySlugAsync(PageRules.Bogshoppen) : null,
            slug == PageRules.Today ? await pages.BySlugAsync(PageRules.Week) : null,
            page is null ? [] : await pages.ChildrenAsync(slug),
            parent);
    }

    public async Task<Page?> PageBySlugAsync(string slug) => await pages.BySlugAsync(slug);

    public async Task<TodayView> TodayAsync() => new(
        await settings.GetAsync(),
        (await days.AllAsync())
            .Where(n => n.HasText && DateTime.TryParse(n.Day, out _))
            .ToDictionary(n => DateTime.Parse(n.Day), n => n));

    public Task<List<AppLink>> ShownAppsAsync() => apps.ShownAsync();

    // "Lige nu" pictures/film and "Min dag" photos: public only while the front page uses them.
    public async Task<ServedFile?> NowFileAsync(string name)
    {
        var s = await settings.GetAsync();
        return s.ContentTypeOfNowFile(name) is { } type && files.Find(name) is { } path ? new(path, type) : null;
    }

    // Pictures on the pages (Om mig …): only files that a page actually uses.
    public async Task<ServedFile?> PageFileAsync(string name)
    {
        name = Path.GetFileName(name);
        return await pages.AnyUsesAsync(PageRules.FileLink(name)) && files.Find(name) is { } path ? new(path, PageRules.FileContentType(name)) : null;
    }

    /// <param name="size">"t" = thumbnail, "d" = download, else the full file.</param>
    public async Task<ServedFile?> TimelineFileAsync(int id, string size, bool family)
    {
        var m = await posts.MediaAsync(id);
        if (m is null || (m.Post.Visibility == Visibility.Familie && !family)) return null;
        var name = size == "t" && m.ThumbName is not null ? m.ThumbName : m.StoredName;
        return files.Find(name) is { } path
            ? new(path, m.ContentType, size == "d" ? m.FileName : null, m.Post.Visibility == Visibility.Familie)
            : null;
    }
}

// Is each app on "Mine apps" running, and Kontrol's switches for them ("På hjemmesiden").
public sealed class AppLinkUseCases(IAppLinkRepository apps, IAppStatus status)
{
    public sealed record AppState(AppLink Link, bool? Up);

    public async Task<List<AppState>> AllWithStatusAsync() => (await apps.AllAsync()).Select(a => new AppState(a, status.IsUp(a.Url))).ToList();
    public Task<bool> SetAwakeAsync(int id, bool awake) => apps.SetAwakeAsync(id, awake);
    public Task<bool> SetShowAsync(int id, bool show) => apps.SetShowAsync(id, show);
    public bool? IsUp(string url) => status.IsUp(url);

    // First start: the apps that have a public address (texts from Kontrol's apps.json).
    public async Task SeedAsync()
    {
        if (await apps.AnyAsync()) return;
        (string Icon, string Name, string Url, string Desc, bool Show)[] seed =
        [
            ("⚡", "ElPriser", "https://elpriser.itmartin.dk", "Gratis elpriser time for time, hvad maskinerne koster, og push når strømmen er billig.", true),
            ("🔌", "MinElpris", "https://minelpris.itmartin.dk", "Dit eget forbrug fra måleren, apparater og regningen forklaret linje for linje.", true),
            ("🧭", "Forløbet", "https://forloebet.itmartin.dk", "Sags-hjælper: hvad gælder, hvad behøver du ikke, hvad gør du nu.", true),
            ("🩺", "PC-tjek", "https://pctjek.itmartin.dk", "Tjek af PC, mikrofon og kamera – med 'Hvad er galt?'.", true),
            ("🗳️", "Stem", "https://stem.itmartin.dk", "Afstemninger – fx logo-afstemningen.", true),
            ("🪑", "Polstrer-tavlen", "https://polstrertavle.itmartin.dk", "Fælles tavle, hvor polstrere viser deres møbler frem.", true),
            ("🎨", "Billedbehandling", "https://billedbehandling.itmartin.dk", "Lav og redigér billeder med AI.", true),
            ("🔨", "Auktion", "https://auktion.itmartin.dk", "Live-auktion med bud i realtid.", true),
            ("🔎", "Find det igen", "https://finddetigen.itmartin.dk", "Find dine ting igen – hjælp til fokus og hukommelse.", true),
            ("📰", "Nyheder", "https://nyheder.itmartin.dk", "Dagens nyheder samlet kort.", true),
            ("🎯", "R6 Assistant", "https://r6.itmartin.dk", "Rainbow Six: kort, bans, operatører og coach-mode til kampene.", true),
            ("🚀", "Strategi", "https://strategi.itmartin.dk", "Strategi-guides til spil (GalCiv4) – på engelsk.", true),
            ("🖼️", "Galleri", "https://gallery.itmartin.dk", "Familiens og kundernes fotogallerier – og fødselsdagssiderne.", false),
            ("📤", "Upload", "https://upload.itmartin.dk", "Kunder og gæster lægger billeder direkte ind (fx Polstrermesse).", false),
            ("🛋️", "Polstrer", "https://polstrer.itmartin.dk", "Polstrerens procesjournal: tag billede, erfaringsarkiv og AI-forslag.", false),
            ("💿", "Bibliotek", "https://bibliotek.itmartin.dk", "Rippede film og musik, kategorier og rip-station-status.", false),
            ("🎬", "Jellyfin", "https://jellyfin.itmartin.dk", "Afspiller film og musik fra biblioteket på TV, telefon og PC.", false),
            ("🎛️", "Kontrol", "https://kontrol.itmartin.dk", "Tænd og sluk alle apps, se forbrug og backup.", false),
        ];
        await apps.SaveAsync(seed.Select((s, i) => new AppLink { Icon = s.Icon, Name = s.Name, Url = s.Url, Description = s.Desc, Show = s.Show, Sort = (i + 1) * 10 }));
    }
}

// Boxes on the front page from the other apps: Kig med (live now?) and ElPriser (the dishwasher tip).
public sealed class NeighbourUseCases(INeighbourApps neighbours)
{
    public sealed record ApplianceTip(string Name, decimal Kwh, decimal Hours, decimal NowKr, DateTime BestStart, decimal BestKr)
    {
        public decimal Save => NowKr - BestKr;
    }

    public async Task<LiveStatus?> LiveAsync()
    {
        try { return await neighbours.KigMedAsync(); }
        catch { return null; }   // photoserver asleep or Kig med down: show nothing
    }

    public async Task<ApplianceTip?> DishwasherTipAsync()
    {
        try
        {
            var all = await neighbours.ElPriserAppliancesAsync();
            var a = all?.FirstOrDefault(x => x.Name == "Opvaskemaskine") ?? all?.FirstOrDefault();
            if (a?.Now is null) return null;
            // The cheapest of today's rest and tomorrow (tomorrow's prices come in the afternoon).
            var best = new[] { a.CheapestToday, a.CheapestTomorrow }.Where(w => w is not null).MinBy(w => w!.CostKr) ?? a.Now;
            return new ApplianceTip(a.Name, a.KwhPerRun, a.DurationHours, a.Now.CostKr, best.Start, best.CostKr);
        }
        catch { return null; }   // ElPriser down: show nothing
    }
}

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ITMartinSuite.Maui.Models;

namespace ITMartinSuite.Maui.ViewModels;

public partial class HomeViewModel : ObservableObject
{
    // Base URL for NAS-hosted apps — change to NAS IP for real device
    private const string NasBase = "http://10.0.2.2";

    public List<AppCategory> Categories { get; } =
    [
        new()
        {
            Name = "Familie & Fællesskab",
            Apps =
            [
                new() { Name = "Familie Overblik", Icon = "👨‍👩‍👦", Description = "Dagens opgaver for hele familien. Tag billeder, tildel og afslut opgaver.", Tags = "Familie · Opgaver · Billeder", GradientStart = "#1a1020", GradientEnd = "#4a2060", MauiRoute = "//familie/board" },
                new() { Name = "Familie", Icon = "🏠", Description = "Fælles opgavetavle for hele familien.", Tags = "Familie · Opgaver · Planlægning", GradientStart = "#1a1020", GradientEnd = "#4a2060", WebUrl = $"{NasBase}:5290" },
                new() { Name = "FindIt", Icon = "🧠", Description = "Husk hvor du lagde tingene. Tag et billede og find det igen.", Tags = "ADHD · Huskeseddel · Genstande", GradientStart = "#1a2030", GradientEnd = "#1a4060", WebUrl = $"{NasBase}:5XXX" },
                new() { Name = "Club", Icon = "🤝", Description = "Fælles platform for grupper og foreninger.", Tags = "Forening · Dokumenter · Kalender", GradientStart = "#101a20", GradientEnd = "#1a3a50", WebUrl = $"{NasBase}:5XXX" },
            ]
        },
        new()
        {
            Name = "Økonomi",
            Apps =
            [
                new() { Name = "Budget", Icon = "💰", Description = "Hold styr på din økonomi. Registrer udgifter og få overblik.", Tags = "Økonomi · Familie · Oversigt", GradientStart = "#1a2a10", GradientEnd = "#2d5a1a", WebUrl = $"{NasBase}:5016" },
                new() { Name = "Kvittering", Icon = "🧾", Description = "Tag et billede af din kvittering — AI udtrækker varelisten.", Tags = "Kvitteringer · AI · Dagligvarer", GradientStart = "#1a2010", GradientEnd = "#3d5a0a", WebUrl = $"{NasBase}:5200" },
            ]
        },
        new()
        {
            Name = "Samlinger",
            Apps =
            [
                new() { Name = "Magic", Icon = "🃏", Description = "Scan og administrer din Magic: The Gathering kortsamling.", Tags = "MTG · Samling · Scanner", GradientStart = "#200a0a", GradientEnd = "#6b1a1a", WebUrl = $"{NasBase}:5020" },
                new() { Name = "Scan Bøger", Icon = "📚", Description = "Scan en bogreol — AI identificerer og registrerer alle bøger.", Tags = "Bøger · AI · Kamera", GradientStart = "#0a1a30", GradientEnd = "#1a3a6a", WebUrl = $"{NasBase}:5143" },
                new() { Name = "Søg Bøger", Icon = "🔍", Description = "Søg i alle bøger på tværs af hele samlingen.", Tags = "Bøger · Søgning · Samling", GradientStart = "#0a1520", GradientEnd = "#0a3050", WebUrl = $"{NasBase}:5XXX" },
            ]
        },
        new()
        {
            Name = "Medier & Filer",
            Apps =
            [
                new() { Name = "FileSorter", Icon = "🗂️", Description = "Sorter og organiser dine mediefiler automatisk med AI.", Tags = "Billeder · Video · AI", GradientStart = "#1a1f35", GradientEnd = "#0d3b6e", WebUrl = $"{NasBase}:5293" },
                new() { Name = "Galleri", Icon = "🖼️", Description = "Gennemse og del dit billedgalleri med familie og venner.", Tags = "Billeder · Video · Deling", GradientStart = "#1a2820", GradientEnd = "#0d4a2e", WebUrl = $"{NasBase}:5XXX" },
            ]
        },
        new()
        {
            Name = "Musik",
            Apps =
            [
                new() { Name = "Martin Musik", Icon = "🎵", Description = "Øv dine egne sange med tekst, akkorder og AI-coaching.", Tags = "Musik · Akkorder · AI Coach", GradientStart = "#1e1030", GradientEnd = "#4a1a7a", WebUrl = $"{NasBase}:5XXX" },
            ]
        },
        new()
        {
            Name = "Sjov & Underholdning",
            Apps =
            [
                new() { Name = "BarTab", Icon = "🍺", Description = "Hold styr på drinks og regninger til fester.", Tags = "Bar · Drinks · Regninger", GradientStart = "#1a1500", GradientEnd = "#5a4500", WebUrl = $"{NasBase}:5XXX" },
                new() { Name = "Auktion", Icon = "🔨", Description = "Afhold live auktioner med realtidsbud fra telefonen.", Tags = "Auktion · Bud · Live", GradientStart = "#1a0a00", GradientEnd = "#5a2000", WebUrl = $"{NasBase}:5XXX" },
                new() { Name = "Marked", Icon = "🏪", Description = "Køb og sælg ting i dit netværk.", Tags = "Køb · Salg · Netværk", GradientStart = "#001a10", GradientEnd = "#004a30", WebUrl = $"{NasBase}:5168" },
            ]
        },
        new()
        {
            Name = "Kvalitetssikring",
            Apps =
            [
                new() { Name = "TestHub", Icon = "🧪", Description = "Koordiner manuelle tests af alle ITMartin-apps.", Tags = "Test · Kvalitet · Feedback", GradientStart = "#0a1a10", GradientEnd = "#1a4020", WebUrl = $"{NasBase}:5XXX" },
            ]
        },
    ];

    [RelayCommand]
    private async Task OpenAppAsync(AppEntry app)
    {
        if (app.MauiRoute is not null)
        {
            await Shell.Current.GoToAsync(app.MauiRoute);
            return;
        }

        if (app.WebUrl is not null && !app.WebUrl.Contains("5XXX"))
            await Browser.OpenAsync(app.WebUrl, BrowserLaunchMode.SystemPreferred);
    }
}

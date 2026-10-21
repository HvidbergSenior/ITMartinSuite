using ITMartinElPriser.Domain;

namespace ITMartinElPriser.Application;

/// <summary>The prices the front page shows, for one household: today and tomorrow per quarter, priced with its own
/// tariffs. The machine's cost at a start and the cheapest start are worked out by the Domain (PriceModel).</summary>
public sealed class NowView(HouseholdSettings settings, List<PricePoint> raw, PriceSnapshot snapshot)
{
    public HouseholdSettings Settings { get; } = settings;
    public PriceSnapshot Snapshot { get; } = snapshot;
    public bool HasPrices => raw.Count > 0;

    /// <summary>The machine the visitor chose (dishwasher or washing machine) with its own hours and kWh.</summary>
    public HomeMachine Machine => (Settings.Machine == HomeMachine.WashKind ? Settings.Wash : Settings.Dish).Clean();

    /// <summary>The machine started at this quarter, priced quarter by quarter while it runs. Null = not all prices known.</summary>
    public RunOption? RunAt(DateTime start) => raw.Count == 0 ? null : PriceModel.CostOfRun(raw, Settings, Machine.ToAppliance(), start);

    /// <summary>The cheapest start from this quarter on; night = also while people sleep (a machine with a delayed start).</summary>
    public RunOption? Cheapest(DateTime from, bool night) =>
        raw.Count == 0 ? null : PriceModel.CheapestFrom(raw, Settings, Machine.ToAppliance(), from, night);

    public bool IsQuiet(DateTime t) => PriceModel.IsQuiet(t, Settings);
}

/// <summary>Where the visitor's home is - the grid company there decides the price.</summary>
public abstract record HomeAnswer
{
    /// <summary>Found and saved.</summary>
    public sealed record Saved(GridCompany Grid) : HomeAnswer;
    /// <summary>The phone is somewhere with another grid company than the saved home - ask before switching.</summary>
    public sealed record AskSwitch(Place Place, GridCompany Grid) : HomeAnswer;
    /// <summary>Something went wrong; Message says what to do, in Danish.</summary>
    public sealed record Problem(string Message) : HomeAnswer;
}

/// <summary>ElPriser's front page (user 2026-09-25: "price now + should I wait + chart" in 5-10 seconds): the prices for
/// this household, its machine, "Hvad havde du sparet?", and finding the home's grid company. The page only shows.</summary>
public sealed class FrontPageUseCases(IPriceSource prices, IHouseholdPrefs prefs, IGridTariffSource grids)
{
    public async Task<NowView> LoadAsync(CancellationToken ct = default)
    {
        var s = await prefs.GetAsync();
        var raw = await prices.GetPricesAsync(s.PriceArea, ct);
        return new NowView(s, raw, PriceModel.Build(raw, s, [], DkTime.Now));
    }

    /// <summary>The 30 days before today with real prices: the machine at 18:00 every day vs at the day's cheapest start.
    /// Today's prices are fetched too, so a late start that runs past midnight is priced. Null = not enough days.</summary>
    public async Task<Hindsight?> HindsightAsync(HouseholdSettings s, CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DkTime.Now);
        var raw = await prices.GetRangeAsync(today.AddDays(-30), today.AddDays(1), s.PriceArea, ct);
        var machine = (s.Machine == HomeMachine.WashKind ? s.Wash : s.Dish).Clean();
        return Hindsight.For(raw, s, machine.ToAppliance(), today);
    }

    /// <summary>What the prices are worked out with, for Besked: "DK1 · N1 · NRGi Time".</summary>
    public string Describe() => HouseholdText.Describe(prefs.Get());

    // ---- the machine (kept in the cookie on the phone) ----

    public Task PickMachineAsync(HouseholdSettings s, string kind)
    {
        s.Machine = kind == HomeMachine.WashKind ? HomeMachine.WashKind : HomeMachine.DishKind;
        return prefs.SaveAsync(s);
    }

    public Task SetMachineAsync(HouseholdSettings s, double? hours, double? kwh)
    {
        var m = s.Machine == HomeMachine.WashKind ? s.Wash : s.Dish;
        if (hours is > 0) m.Hours = hours.Value;
        if (kwh is > 0) m.Kwh = kwh.Value;
        m.Clean();
        return prefs.SaveAsync(s);
    }

    public Task ResetMachineAsync(HouseholdSettings s)
    {
        if (s.Machine == HomeMachine.WashKind) s.Wash = HomeMachine.WashingMachine(); else s.Dish = HomeMachine.Dishwasher();
        return prefs.SaveAsync(s);
    }

    // ---- home ----

    /// <summary>A typed address or postcode. Being typed, it IS the home - saved at once.</summary>
    public async Task<HomeAnswer> FindAddressAsync(string text)
    {
        if (text.Trim().Length < 4) return new HomeAnswer.Problem("Skriv en adresse eller et postnummer (4 cifre).");
        if (await grids.ResolveAsync(text.Trim()) is not { } place)
            return new HomeAnswer.Problem("Vi kunne ikke finde den adresse. Prøv med vej, husnummer og postnummer – eller kun postnummeret.");
        if (await grids.FindAtAsync(place.Lat, place.Lon) is not { } g)
            return new HomeAnswer.Problem("Vi kunne ikke finde netselskabet lige nu. Prøv igen om lidt.");
        await SaveHomeAsync(place, g);
        return new HomeAnswer.Saved(g);
    }

    /// <summary>The phone's location ("Jeg er hjemme nu"). What matters is the grid company at HOME: when one is saved and
    /// this place has another, the visitor is asked first (askingForNew = they pressed "skift", then no question).</summary>
    public async Task<HomeAnswer> AtLocationAsync(double lat, double lon, bool askingForNew)
    {
        var saved = prefs.Get();
        if (await grids.ReverseAsync(lat, lon) is not { } place)
            return new HomeAnswer.Problem("Du ser ud til at være uden for Danmark. ElPriser viser danske elpriser – dit netselskab findes, når du er hjemme igen."
                + (saved.GridName.Length > 0 ? $" Vi bruger stadig {saved.GridName}." : " Indtil da vises priserne uden din nettarif."));
        if (await grids.FindAtAsync(place.Lat, place.Lon) is not { } g)
            return new HomeAnswer.Problem("Vi kunne ikke finde netselskabet for din placering lige nu. Prøv igen om lidt.");
        if (saved.GridSupplierId.Length > 0 && saved.GridSupplierId != g.Id && !askingForNew) return new HomeAnswer.AskSwitch(place, g);
        await SaveHomeAsync(place, g);
        return new HomeAnswer.Saved(g);
    }

    /// <summary>Why the phone gave no location, and what to do - never a silent nothing (user 2026-10-05).</summary>
    public static string LocationProblem(int error) => error switch
    {
        1 => "Telefonen må ikke give ElPriser din placering. Tryk på ikonet til venstre for adressen øverst → Tilladelser → Placering → Tillad, og prøv igen. Eller skriv din adresse herunder.",
        8 => "Du har åbnet ElPriser inde i Facebook, og Facebook giver ikke din placering videre. Tryk på ⋮ øverst til højre → Åbn i Chrome (Open in browser), og tryk så her igen. Eller skriv din adresse herunder.",
        3 => "Telefonen var for længe om at finde din placering. Prøv igen – eller skriv din adresse herunder.",
        9 => "Din browser kan ikke give en placering. Skriv din adresse eller dit postnummer herunder.",
        _ => "Telefonen kunne ikke finde din placering. Slå Placering til (træk ned fra toppen af skærmen → Placering), og prøv igen. Eller skriv din adresse herunder.",
    };

    public async Task SaveHomeAsync(Place place, GridCompany g)
    {
        var s = prefs.Get();
        s.PostalCode = place.PostalCode;
        s.GridSupplierId = g.Id;
        s.GridName = g.Name;
        s.PriceArea = g.PriceArea;
        s.NettarifLookup = null;
        await prefs.SaveAsync(s);
    }
}

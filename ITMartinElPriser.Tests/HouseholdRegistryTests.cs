using ITMartinMitEl.Server.Data;
using ITMartinMitEl.Server.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ITMartinElPriser.Tests;

/// <summary>
/// The guarantee the whole accounts change rests on: one household's data never
/// leaks into another's, and what a page writes survives a restart.
/// </summary>
public class HouseholdRegistryTests
{
    private string _dbPath = "";
    private ServiceProvider _services = null!;
    private IDbContextFactory<MitElDbContext> _factory = null!;
    private HouseholdRegistry _registry = null!;

    [SetUp]
    public void SetUp()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"mitel-reg-{Guid.NewGuid():N}.db");
        var services = new ServiceCollection();
        services.AddDbContextFactory<MitElDbContext>(o => o.UseSqlite($"Data Source={_dbPath}"));
        _services = services.BuildServiceProvider();
        _factory = _services.GetRequiredService<IDbContextFactory<MitElDbContext>>();
        using (var db = _factory.CreateDbContext()) MitElSchema.Ensure(db);
        _registry = new HouseholdRegistry(_factory);
    }

    [TearDown]
    public void TearDown()
    {
        _services.Dispose();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }

    private Guid NewHousehold(string name)
    {
        using var db = _factory.CreateDbContext();
        var household = new Household { Name = name };
        db.Households.Add(household);
        db.SaveChanges();
        return household.Id;
    }

    [Test]
    public void Two_households_keep_their_own_settings()
    {
        var annette = NewHousehold("Hos Annette");
        var kent = NewHousehold("Hos Kent");

        _registry.Household(annette).Update(d => d.Settings.EloverblikToken = "annettes-token");
        _registry.Household(kent).Update(d => d.Settings.EloverblikToken = "kents-token");

        Assert.That(_registry.Household(annette).Get().Settings.EloverblikToken, Is.EqualTo("annettes-token"));
        Assert.That(_registry.Household(kent).Get().Settings.EloverblikToken, Is.EqualTo("kents-token"));
    }

    [Test]
    public void What_a_page_writes_is_still_there_after_a_restart()
    {
        var household = NewHousehold("Hos Annette");
        _registry.Household(household).Update(d => d.Settings.MeteringPointId = "571313174000000000");

        var afterRestart = new HouseholdRegistry(_factory);

        Assert.That(afterRestart.Household(household).Get().Settings.MeteringPointId, Is.EqualTo("571313174000000000"));
    }

    [Test]
    public void Consumption_is_per_household_too()
    {
        var annette = NewHousehold("Hos Annette");
        var kent = NewHousehold("Hos Kent");

        _registry.Consumption(annette).Merge([new EloverblikService.HourReading(new DateTime(2026, 9, 20, 7, 0, 0), 1.25, "measured")]);

        Assert.That(_registry.Consumption(annette).HasAny, Is.True);
        Assert.That(_registry.Consumption(kent).HasAny, Is.False);
    }

    [Test]
    public void Every_household_is_listed_for_the_background_jobs()
    {
        var annette = NewHousehold("Hos Annette");
        var kent = NewHousehold("Hos Kent");

        Assert.That(_registry.AllHouseholdIds(), Is.EquivalentTo(new[] { annette, kent }));
    }

    [Test]
    public void A_household_that_no_longer_exists_does_not_crash_a_write()
    {
        var household = NewHousehold("Hos Annette");
        var store = _registry.Household(household);

        using (var db = _factory.CreateDbContext())
        {
            db.Households.Remove(db.Households.Find(household)!);
            db.SaveChanges();
        }

        Assert.DoesNotThrow(() => store.Update(d => d.Settings.PriceArea = "DK1"));
    }
}

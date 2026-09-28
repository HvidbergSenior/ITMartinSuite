using ITMartinMitEl.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace ITMartinMitEl.Server.Services;

/// <summary>
/// "👀 Se demo" from the free ElPriser: MinElpris opened as a demo household with real numbers, so
/// people can see what the paid version gives before they sign up (user 2026-09-28: "something with
/// a wow effect"). A version of the one app, not a separate deployment (user rule).
///
/// At every start the demo household is rebuilt from the owner's household, with everything
/// personal removed (eloverblik token, meter ids, push subscribers). It is read-only: the registry
/// never writes it back, so a visitor's clicks never reach the database or other visitors' view
/// after the next restart.
/// </summary>
public static class DemoHousehold
{
    public const string Email = "demo@minelpris.demo";
    public const string Name = "Demo – familie i Aarhus";

    public static Guid? Id { get; private set; }

    public static void Ensure(IServiceProvider services, IConfiguration config, ILogger logger)
    {
        try
        {
            var factory = services.GetRequiredService<IDbContextFactory<MitElDbContext>>();
            using var db = factory.CreateDbContext();

            var sourceEmail = AccountService.Normalise(config["MitEl:DemoSourceEmail"] ?? "itmartin@mensa.dk");
            var source = db.Users.FirstOrDefault(u => u.Email == sourceEmail && u.HouseholdId != null);
            var sourceRow = source is null ? null : db.Households.Find(source.HouseholdId);
            if (sourceRow is null) { logger.LogWarning("Demo: no source household for {Email}", sourceEmail); return; }

            var data = HouseholdStore.Parse(sourceRow.DataJson);
            data.Settings.EloverblikToken = "";
            data.Settings.MeteringPointId = "";
            data.Settings.MeteringPointAddress = "";
            data.Subscribers.Clear();
            data.SentPushes.Clear();
            data.Bills = data.Bills.Select(b => b with { MeterNumber = "", MeteringPointId = "" }).ToList();

            var user = db.Users.FirstOrDefault(u => u.Email == Email);
            var row = user?.HouseholdId is { } hid ? db.Households.Find(hid) : null;
            if (row is null)
            {
                row = new Household { Name = Name };
                db.Households.Add(row);
            }
            row.Name = Name;
            row.DataJson = System.Text.Json.JsonSerializer.Serialize(data);
            row.ConsumptionJson = sourceRow.ConsumptionJson;

            if (user is null)
            {
                user = new UserAccount
                {
                    Email = Email,
                    // Nobody logs in with a password - /demo signs in directly.
                    PasswordHash = PasswordHasher.Hash(Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N")),
                    Role = UserRoles.Owner,
                    DisplayName = "Demo",
                };
                db.Users.Add(user);
            }
            user.HouseholdId = row.Id;
            db.SaveChanges();
            Id = row.Id;
            logger.LogInformation("Demo household {Id} rebuilt from {Email}", row.Id, sourceEmail);
        }
        catch (Exception ex)
        {
            // The demo is a nice-to-have; it must never stop the app from starting.
            logger.LogWarning(ex, "Demo household could not be built");
        }
    }

    public static bool IsDemo(Guid householdId) => Id == householdId;
}

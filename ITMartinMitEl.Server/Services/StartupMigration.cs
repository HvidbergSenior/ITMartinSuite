using System.Security.Cryptography;
using ITMartinMitEl.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace ITMartinMitEl.Server.Services;

/// <summary>
/// Moves the single-household app into the accounts world on first start: the existing
/// household.json and consumption.json become household #1 with an owner account, and
/// Martin's advisor account is created if it is configured. Runs once; afterwards it
/// finds the database already populated and does nothing.
/// </summary>
public static class StartupMigration
{
    public static void Run(IServiceProvider services, IConfiguration config, ILogger logger)
    {
        var factory = services.GetRequiredService<IDbContextFactory<MitElDbContext>>();
        using var db = factory.CreateDbContext();
        MitElSchema.Ensure(db);

        var dataDir = config["DataDir"] ?? "/data";
        ImportExistingHousehold(db, config, dataDir, logger);
        EnsureAdvisor(db, config, logger);
    }

    private static void ImportExistingHousehold(MitElDbContext db, IConfiguration config, string dataDir, ILogger logger)
    {
        if (db.Households.Any()) return;

        var householdFile = Path.Combine(dataDir, "household.json");
        var consumptionFile = Path.Combine(dataDir, "consumption.json");
        var hasOldData = File.Exists(householdFile);

        var household = new Household
        {
            Name = config["MitEl:OwnerHouseholdName"] ?? "Skelagervej",
            DataJson = hasOldData ? ReadOrEmpty(householdFile) : "",
            ConsumptionJson = File.Exists(consumptionFile) ? ReadOrEmpty(consumptionFile) : "",
        };

        var email = AccountService.Normalise(config["MitEl:OwnerEmail"] ?? "ITMartin@Mensa.dk");
        var password = config["MitEl:OwnerPassword"];
        var generated = false;
        if (string.IsNullOrWhiteSpace(password) || password.Length < AccountService.MinPasswordLength)
        {
            password = GeneratePassword();
            generated = true;
        }

        db.Households.Add(household);
        db.Users.Add(new UserAccount
        {
            Email = email,
            PasswordHash = PasswordHasher.Hash(password),
            Role = UserRoles.Owner,
            HouseholdId = household.Id,
            DisplayName = "Martin",
        });
        db.SaveChanges();

        if (generated)
        {
            // Nobody can read a log line they never see, and there is no mail server
            // to send it with, so the first password is also written next to the data.
            var file = Path.Combine(dataDir, "FOERSTE-ADGANGSKODE.txt");
            File.WriteAllText(file,
                $"MinElpris ejerkonto\r\nMail: {email}\r\nAdgangskode: {password}\r\n\r\n" +
                "Skift den under Konto, og slet så denne fil.\r\n");
            logger.LogWarning("KOLIBRI-MITEL: owner account {Email} created with a generated password, written to {File}", email, file);
        }

        logger.LogInformation("KOLIBRI-MITEL: imported {What} as household {Id}",
            hasOldData ? "household.json" : "an empty household", household.Id);
    }

    private static void EnsureAdvisor(MitElDbContext db, IConfiguration config, ILogger logger)
    {
        var email = AccountService.Normalise(config["MitEl:AdvisorEmail"] ?? "");
        var password = config["MitEl:AdvisorPassword"] ?? "";
        if (email.Length == 0 || password.Length < AccountService.MinPasswordLength) return;
        if (db.Users.Any(u => u.Email == email)) return;

        db.Users.Add(new UserAccount
        {
            Email = email,
            PasswordHash = PasswordHasher.Hash(password),
            Role = UserRoles.Advisor,
            HouseholdId = null,
            DisplayName = config["MitEl:AdvisorName"] ?? "ITMartin",
        });
        db.SaveChanges();
        logger.LogInformation("KOLIBRI-MITEL: advisor account {Email} created", email);
    }

    private static string ReadOrEmpty(string path)
    {
        try { return File.ReadAllText(path); }
        catch (IOException) { return ""; }
    }

    private static string GeneratePassword()
    {
        const string alphabet = "abcdefghijkmnopqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var chars = new char[14];
        for (var i = 0; i < chars.Length; i++)
            chars[i] = alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
        return new string(chars);
    }
}

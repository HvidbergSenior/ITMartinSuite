using ITMartinMitEl.Server.Data;
using ITMartinMitEl.Server.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ITMartinElPriser.Tests;

/// <summary>
/// Accounts, households and advisor grants. Every test gets its own SQLite file, so
/// the rules are checked against the database the app actually runs on.
/// </summary>
public class AccountsTests
{
    private string _dbPath = "";
    private ServiceProvider _services = null!;
    private AccountService _accounts = null!;
    private AdvisorService _advisors = null!;
    private IDbContextFactory<MitElDbContext> _factory = null!;

    [SetUp]
    public void SetUp()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"mitel-tests-{Guid.NewGuid():N}.db");

        var services = new ServiceCollection();
        services.AddDbContextFactory<MitElDbContext>(o => o.UseSqlite($"Data Source={_dbPath}"));
        _services = services.BuildServiceProvider();

        _factory = _services.GetRequiredService<IDbContextFactory<MitElDbContext>>();
        using (var db = _factory.CreateDbContext()) MitElSchema.Ensure(db);

        _accounts = new AccountService(_factory);
        _advisors = new AdvisorService(_factory);
    }

    [TearDown]
    public void TearDown()
    {
        _services.Dispose();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }

    // ── passwords ──────────────────────────────────────────────────────────

    [Test]
    public void A_password_verifies_against_its_own_hash()
    {
        var hash = PasswordHasher.Hash("hemmeligt123");

        Assert.That(PasswordHasher.Verify("hemmeligt123", hash), Is.True);
        Assert.That(PasswordHasher.Verify("Hemmeligt123", hash), Is.False);
        Assert.That(PasswordHasher.Verify("", hash), Is.False);
    }

    [Test]
    public void The_same_password_hashes_differently_every_time()
    {
        Assert.That(PasswordHasher.Hash("hemmeligt123"), Is.Not.EqualTo(PasswordHasher.Hash("hemmeligt123")));
    }

    [Test]
    public void A_damaged_hash_never_verifies()
    {
        Assert.That(PasswordHasher.Verify("hemmeligt123", "noget-vrøvl"), Is.False);
        Assert.That(PasswordHasher.Verify("hemmeligt123", ""), Is.False);
    }

    // ── registration ───────────────────────────────────────────────────────

    [Test]
    public async Task Registering_creates_the_account_and_its_household()
    {
        var result = await _accounts.RegisterAsync("Annette@example.dk", "elpriser2026", "Annette", "Hos Annette");

        Assert.That(result.Ok, Is.True);
        Assert.That(result.User!.Email, Is.EqualTo("annette@example.dk"), "the mail is the login name, so it is stored lowercase");
        Assert.That(result.User.HouseholdId, Is.Not.Null);

        await using var db = _factory.CreateDbContext();
        Assert.That(db.Households.Single().Name, Is.EqualTo("Hos Annette"));
    }

    [Test]
    public async Task The_same_mail_cannot_register_twice()
    {
        await _accounts.RegisterAsync("annette@example.dk", "elpriser2026", "Annette");

        var second = await _accounts.RegisterAsync("ANNETTE@example.dk", "andetkode1", "Annette igen");

        Assert.That(second.Ok, Is.False);
        Assert.That(second.Error, Does.Contain("allerede"));
    }

    [TestCase("ikke-en-mail", "elpriser2026")]
    [TestCase("annette@example.dk", "kort")]
    public async Task Rubbish_is_refused(string email, string password)
    {
        var result = await _accounts.RegisterAsync(email, password, "Annette");

        Assert.That(result.Ok, Is.False);
    }

    // ── login ──────────────────────────────────────────────────────────────

    [Test]
    public async Task The_right_password_signs_in()
    {
        await _accounts.RegisterAsync("annette@example.dk", "elpriser2026", "Annette");

        var result = await _accounts.AuthenticateAsync("Annette@example.dk", "elpriser2026");

        Assert.That(result.Ok, Is.True);
        Assert.That(result.User!.LastLoginUtc, Is.Not.Null);
    }

    [Test]
    public async Task A_wrong_password_and_an_unknown_mail_answer_the_same()
    {
        await _accounts.RegisterAsync("annette@example.dk", "elpriser2026", "Annette");

        var wrongPassword = await _accounts.AuthenticateAsync("annette@example.dk", "forkert1234");
        var unknownMail = await _accounts.AuthenticateAsync("ukendt@example.dk", "elpriser2026");

        Assert.That(wrongPassword.Error, Is.EqualTo(unknownMail.Error),
            "the page must not reveal whether a mail exists");
    }

    [Test]
    public async Task Changing_the_password_needs_the_old_one()
    {
        var user = (await _accounts.RegisterAsync("annette@example.dk", "elpriser2026", "Annette")).User!;

        Assert.That((await _accounts.ChangePasswordAsync(user.Id, "forkert1234", "nytkodeord1")).Ok, Is.False);
        Assert.That((await _accounts.ChangePasswordAsync(user.Id, "elpriser2026", "nytkodeord1")).Ok, Is.True);
        Assert.That((await _accounts.AuthenticateAsync("annette@example.dk", "nytkodeord1")).Ok, Is.True);
    }

    [Test]
    public async Task Martin_can_reset_a_forgotten_password()
    {
        await _accounts.RegisterAsync("annette@example.dk", "elpriser2026", "Annette");

        var reset = await _accounts.ResetPasswordAsync("annette@example.dk", "midlertidig1");

        Assert.That(reset.Ok, Is.True);
        Assert.That((await _accounts.AuthenticateAsync("annette@example.dk", "midlertidig1")).Ok, Is.True);
    }

    // ── deleting ───────────────────────────────────────────────────────────

    [Test]
    public async Task Deleting_an_account_takes_the_household_and_the_grants_with_it()
    {
        var user = (await _accounts.RegisterAsync("annette@example.dk", "elpriser2026", "Annette")).User!;
        await _advisors.CreateCodeAsync(user.HouseholdId!.Value);

        Assert.That(await _accounts.DeleteAccountAsync(user.Id), Is.True);

        await using var db = _factory.CreateDbContext();
        Assert.That(db.Users.Any(), Is.False);
        Assert.That(db.Households.Any(), Is.False);
        Assert.That(db.AdvisorAccess.Any(), Is.False);
    }

    [Test]
    public async Task An_advisor_leaving_does_not_delete_the_customers_data()
    {
        var customer = (await _accounts.RegisterAsync("annette@example.dk", "elpriser2026", "Annette")).User!;
        var advisor = (await _accounts.RegisterAdvisorAsync("martin@example.dk", "raadgiver123", "Martin")).User!;
        var code = await _advisors.CreateCodeAsync(customer.HouseholdId!.Value);
        await _advisors.RedeemAsync(code, advisor.Id);

        await _accounts.DeleteAccountAsync(advisor.Id);

        await using var db = _factory.CreateDbContext();
        Assert.That(db.Households.Any(), Is.True, "the customer's home must survive");
        Assert.That(await _advisors.HasAccessAsync(advisor.Id, customer.HouseholdId.Value), Is.False);
    }

    // ── advisor access ─────────────────────────────────────────────────────

    [Test]
    public async Task An_advisor_sees_nothing_until_a_code_is_redeemed()
    {
        var customer = (await _accounts.RegisterAsync("annette@example.dk", "elpriser2026", "Annette")).User!;
        var advisor = (await _accounts.RegisterAdvisorAsync("martin@example.dk", "raadgiver123", "Martin")).User!;
        var householdId = customer.HouseholdId!.Value;

        Assert.That(await _advisors.HasAccessAsync(advisor.Id, householdId), Is.False);

        var code = await _advisors.CreateCodeAsync(householdId);
        var (ok, _) = await _advisors.RedeemAsync(code, advisor.Id);

        Assert.That(ok, Is.True);
        Assert.That(await _advisors.HasAccessAsync(advisor.Id, householdId), Is.True);
    }

    [Test]
    public async Task A_code_works_once()
    {
        var customer = (await _accounts.RegisterAsync("annette@example.dk", "elpriser2026", "Annette")).User!;
        var first = (await _accounts.RegisterAdvisorAsync("martin@example.dk", "raadgiver123", "Martin")).User!;
        var second = (await _accounts.RegisterAdvisorAsync("anden@example.dk", "raadgiver123", "Anden")).User!;
        var code = await _advisors.CreateCodeAsync(customer.HouseholdId!.Value);

        await _advisors.RedeemAsync(code, first.Id);
        var (ok, message) = await _advisors.RedeemAsync(code, second.Id);

        Assert.That(ok, Is.False);
        Assert.That(message, Does.Contain("brugt"));
        Assert.That(await _advisors.HasAccessAsync(second.Id, customer.HouseholdId.Value), Is.False);
    }

    [Test]
    public async Task A_code_written_with_a_dash_or_in_lowercase_still_works()
    {
        var customer = (await _accounts.RegisterAsync("annette@example.dk", "elpriser2026", "Annette")).User!;
        var advisor = (await _accounts.RegisterAdvisorAsync("martin@example.dk", "raadgiver123", "Martin")).User!;
        var code = await _advisors.CreateCodeAsync(customer.HouseholdId!.Value);

        var (ok, _) = await _advisors.RedeemAsync(AdvisorService.Pretty(code).ToLowerInvariant(), advisor.Id);

        Assert.That(ok, Is.True);
    }

    [Test]
    public async Task An_unknown_code_is_refused()
    {
        var advisor = (await _accounts.RegisterAdvisorAsync("martin@example.dk", "raadgiver123", "Martin")).User!;

        var (ok, message) = await _advisors.RedeemAsync("XXXXYYYY", advisor.Id);

        Assert.That(ok, Is.False);
        Assert.That(message, Does.Contain("findes ikke"));
    }

    [Test]
    public async Task An_expired_code_is_refused()
    {
        var customer = (await _accounts.RegisterAsync("annette@example.dk", "elpriser2026", "Annette")).User!;
        var advisor = (await _accounts.RegisterAdvisorAsync("martin@example.dk", "raadgiver123", "Martin")).User!;
        var code = await _advisors.CreateCodeAsync(customer.HouseholdId!.Value);

        await using (var db = _factory.CreateDbContext())
        {
            var grant = db.AdvisorAccess.Single(a => a.Code == code);
            grant.ExpiresUtc = DateTime.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
        }

        var (ok, message) = await _advisors.RedeemAsync(code, advisor.Id);

        Assert.That(ok, Is.False);
        Assert.That(message, Does.Contain("udløbet"));
    }

    [Test]
    public async Task The_customer_can_take_the_access_back()
    {
        var customer = (await _accounts.RegisterAsync("annette@example.dk", "elpriser2026", "Annette")).User!;
        var advisor = (await _accounts.RegisterAdvisorAsync("martin@example.dk", "raadgiver123", "Martin")).User!;
        var householdId = customer.HouseholdId!.Value;
        var code = await _advisors.CreateCodeAsync(householdId);
        await _advisors.RedeemAsync(code, advisor.Id);

        var grant = (await _advisors.GrantsForHouseholdAsync(householdId)).Single();
        Assert.That(await _advisors.RevokeAsync(householdId, grant.Id), Is.True);

        Assert.That(await _advisors.HasAccessAsync(advisor.Id, householdId), Is.False);
        Assert.That((await _advisors.CustomersAsync(advisor.Id)), Is.Empty);
    }

    [Test]
    public async Task One_customer_cannot_revoke_another_customers_grant()
    {
        var annette = (await _accounts.RegisterAsync("annette@example.dk", "elpriser2026", "Annette")).User!;
        var kent = (await _accounts.RegisterAsync("kent@example.dk", "elpriser2026", "Kent")).User!;
        var advisor = (await _accounts.RegisterAdvisorAsync("martin@example.dk", "raadgiver123", "Martin")).User!;
        var code = await _advisors.CreateCodeAsync(annette.HouseholdId!.Value);
        await _advisors.RedeemAsync(code, advisor.Id);

        var grant = (await _advisors.GrantsForHouseholdAsync(annette.HouseholdId.Value)).Single();
        var revoked = await _advisors.RevokeAsync(kent.HouseholdId!.Value, grant.Id);

        Assert.That(revoked, Is.False);
        Assert.That(await _advisors.HasAccessAsync(advisor.Id, annette.HouseholdId.Value), Is.True);
    }

    [Test]
    public async Task The_customer_list_shows_only_live_grants()
    {
        var annette = (await _accounts.RegisterAsync("annette@example.dk", "elpriser2026", "Annette")).User!;
        var kent = (await _accounts.RegisterAsync("kent@example.dk", "elpriser2026", "Kent", "Hos Kent")).User!;
        var advisor = (await _accounts.RegisterAdvisorAsync("martin@example.dk", "raadgiver123", "Martin")).User!;

        foreach (var householdId in new[] { annette.HouseholdId!.Value, kent.HouseholdId!.Value })
            await _advisors.RedeemAsync(await _advisors.CreateCodeAsync(householdId), advisor.Id);

        var annetteGrant = (await _advisors.GrantsForHouseholdAsync(annette.HouseholdId.Value)).Single();
        await _advisors.RevokeAsync(annette.HouseholdId.Value, annetteGrant.Id);

        var customers = await _advisors.CustomersAsync(advisor.Id);

        Assert.That(customers.Select(c => c.HouseholdName), Is.EqualTo(new[] { "Hos Kent" }));
    }

    [Test]
    public async Task A_code_is_readable_over_the_phone()
    {
        var customer = (await _accounts.RegisterAsync("annette@example.dk", "elpriser2026", "Annette")).User!;

        var code = await _advisors.CreateCodeAsync(customer.HouseholdId!.Value);

        Assert.That(code, Has.Length.EqualTo(8));
        Assert.That(code, Does.Not.Contain("O").And.Not.Contain("0").And.Not.Contain("I").And.Not.Contain("1"));
        Assert.That(AdvisorService.Pretty(code), Is.EqualTo($"{code[..4]}-{code[4..]}"));
    }

    [Test]
    public async Task A_household_member_logs_in_to_the_same_home_and_leaving_keeps_the_home()
    {
        var owner = (await _accounts.RegisterAsync("ejer@test.dk", "hemmeligt123", "Ejer")).User!;
        var (added, password) = await _accounts.AddMemberAsync(owner.HouseholdId!.Value, "Partner@Test.dk", "Partner");

        Assert.That(added.Ok, Is.True, added.Error);
        var login = await _accounts.AuthenticateAsync("partner@test.dk", password!);
        Assert.That(login.User!.HouseholdId, Is.EqualTo(owner.HouseholdId));
        Assert.That(await _accounts.MembersAsync(owner.HouseholdId.Value), Has.Count.EqualTo(2));

        // The partner leaving must not take the home with them.
        Assert.That(await _accounts.DeleteAccountAsync(login.User.Id), Is.False);
        await using var db = await _factory.CreateDbContextAsync();
        Assert.That(await db.Households.FindAsync(owner.HouseholdId.Value), Is.Not.Null);
        Assert.That(await _accounts.MembersAsync(owner.HouseholdId.Value), Has.Count.EqualTo(1));
    }

    [Test]
    public async Task A_member_cannot_remove_themselves_and_the_email_must_be_free()
    {
        var owner = (await _accounts.RegisterAsync("ejer2@test.dk", "hemmeligt123", "Ejer")).User!;
        var household = owner.HouseholdId!.Value;

        Assert.That(await _accounts.RemoveMemberAsync(household, owner.Id, owner.Id), Is.False);
        var (dupe, _) = await _accounts.AddMemberAsync(household, "ejer2@test.dk", "Igen");
        Assert.That(dupe.Ok, Is.False);
    }
}

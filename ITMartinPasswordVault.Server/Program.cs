using ITMartinPasswordVault.Server.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Security.Cryptography;

var builder = WebApplication.CreateBuilder(args);

// =========================
// DATABASE
// =========================
var connectionString =
    builder.Environment.IsDevelopment()
        ? builder.Configuration.GetConnectionString("VaultDb") ?? "Data Source=vault.db"
        : "Data Source=/app/data/vault.db";

builder.Services.AddDbContext<VaultDbContext>(options => options.UseSqlite(connectionString));

// =========================
// AUTH
// =========================
// Cookie only ever carries an opaque UserId claim, set after the server has
// verified a client-derived proof (bcrypt-checked) - the cookie itself is
// never a bearer of the master password or any encryption key, only "this
// browser already proved it knows the master password once".
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "vault_auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.ExpireTimeSpan = TimeSpan.FromDays(30);
        options.SlidingExpiration = true;
        options.LoginPath = "/login.html";
        options.Events.OnRedirectToLogin = ctx =>
        {
            ctx.Response.StatusCode = 401;
            return Task.CompletedTask;
        };
    });
builder.Services.AddAuthorization();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<VaultDbContext>().Database.EnsureCreated();
}

app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();

// =========================
// Enumeration-resistant salt lookup - server-side HMAC of the email with a
// fixed server secret produces a deterministic but unguessable "fake salt"
// for unknown emails, so /api/salt responds with the same shape either way
// and can't be used to test which emails have an account.
// =========================
var fakeSaltKey = app.Configuration["Vault:FakeSaltKey"] ?? "dev-only-fake-salt-key-change-me";

string FakeSaltFor(string email)
{
    using var hmac = new HMACSHA256(System.Text.Encoding.UTF8.GetBytes(fakeSaltKey));
    return Convert.ToBase64String(hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(email.ToLowerInvariant())));
}

app.MapGet("/api/salt", async (string email, VaultDbContext db) =>
{
    var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email.ToLowerInvariant());
    return Results.Ok(new { salt = user?.Salt ?? FakeSaltFor(email) });
});

app.MapPost("/api/signup", async (SignupRequest body, HttpContext ctx, VaultDbContext db) =>
{
    var email = body.Email.Trim().ToLowerInvariant();
    if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(body.Salt))
        return Results.BadRequest("Manglende felter.");

    if (await db.Users.AnyAsync(u => u.Email == email))
        return Results.Conflict("Denne email er allerede oprettet.");

    var user = new VaultUser
    {
        Email = email,
        Salt = body.Salt,
        AuthHash = BCrypt.Net.BCrypt.HashPassword(body.LoginAuthProof),
        RecoveryAuthHash = BCrypt.Net.BCrypt.HashPassword(body.RecoveryAuthProof),
        WrappedDekByMaster = body.WrappedDekByMaster,
        WrappedDekByMasterIv = body.WrappedDekByMasterIv,
        WrappedDekByRecovery = body.WrappedDekByRecovery,
        WrappedDekByRecoveryIv = body.WrappedDekByRecoveryIv,
    };
    db.Users.Add(user);
    await db.SaveChangesAsync();

    var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, user.Id.ToString()) };
    var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
    await ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));

    return Results.Ok(new { userId = user.Id });
});

app.MapPost("/api/login", async (LoginRequest body, HttpContext ctx, VaultDbContext db) =>
{
    var email = body.Email.Trim().ToLowerInvariant();
    var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
    // Same bcrypt call whether the user exists or not - constant-ish time,
    // avoids a cheap timing signal for account enumeration.
    var hashToCheck = user?.AuthHash ?? BCrypt.Net.BCrypt.HashPassword("dummy");
    var ok = user is not null && BCrypt.Net.BCrypt.Verify(body.LoginAuthProof, hashToCheck);
    if (!ok) return Results.Unauthorized();

    var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, user!.Id.ToString()) };
    var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
    await ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));

    return Results.Ok(new { wrappedDekByMaster = user.WrappedDekByMaster, wrappedDekByMasterIv = user.WrappedDekByMasterIv });
});

app.MapPost("/api/logout", async (HttpContext ctx) =>
{
    await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Ok();
});

app.MapGet("/api/recovery-blob", async (string email, VaultDbContext db) =>
{
    var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email.Trim().ToLowerInvariant());
    if (user is null) return Results.NotFound();
    return Results.Ok(new { wrappedDekByRecovery = user.WrappedDekByRecovery, wrappedDekByRecoveryIv = user.WrappedDekByRecoveryIv });
});

// Resets the master password: only accepted if recoveryAuthProof (derived
// client-side from the DEK obtained by unwrapping WrappedDekByRecovery with
// the user's saved recovery key) matches RecoveryAuthHash - proof the caller
// actually holds the DEK, not just that they typed something into the
// "forgot password" form. The server still never sees the recovery key, the
// DEK, or the new master password - only the new wrapped-DEK-by-master
// blob, which is meaningless without the new master password anyway.
app.MapPost("/api/recover/complete", async (RecoverCompleteRequest body, VaultDbContext db) =>
{
    var email = body.Email.Trim().ToLowerInvariant();
    var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
    if (user is null) return Results.NotFound();

    if (!BCrypt.Net.BCrypt.Verify(body.RecoveryAuthProof, user.RecoveryAuthHash))
        return Results.Unauthorized();

    user.Salt = body.NewSalt;
    user.AuthHash = BCrypt.Net.BCrypt.HashPassword(body.NewLoginAuthProof);
    user.WrappedDekByMaster = body.NewWrappedDekByMaster;
    user.WrappedDekByMasterIv = body.NewWrappedDekByMasterIv;
    await db.SaveChangesAsync();

    return Results.Ok();
});

app.MapGet("/api/vault/entries", async (ClaimsPrincipal user, VaultDbContext db) =>
{
    var userId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
    var entries = await db.Entries.Where(e => e.UserId == userId)
        .OrderByDescending(e => e.UpdatedAt)
        .Select(e => new { e.Id, e.Ciphertext, e.Iv, e.UpdatedAt })
        .ToListAsync();
    return Results.Ok(entries);
}).RequireAuthorization();

app.MapPost("/api/vault/entries", async (VaultEntryRequest body, ClaimsPrincipal user, VaultDbContext db) =>
{
    var userId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
    var entry = new VaultEntry { UserId = userId, Ciphertext = body.Ciphertext, Iv = body.Iv };
    db.Entries.Add(entry);
    await db.SaveChangesAsync();
    return Results.Ok(new { id = entry.Id });
}).RequireAuthorization();

app.MapPut("/api/vault/entries/{id:guid}", async (Guid id, VaultEntryRequest body, ClaimsPrincipal user, VaultDbContext db) =>
{
    var userId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
    var entry = await db.Entries.FirstOrDefaultAsync(e => e.Id == id && e.UserId == userId);
    if (entry is null) return Results.NotFound();
    entry.Ciphertext = body.Ciphertext;
    entry.Iv = body.Iv;
    entry.UpdatedAt = DateTime.UtcNow;
    await db.SaveChangesAsync();
    return Results.Ok();
}).RequireAuthorization();

app.MapDelete("/api/vault/entries/{id:guid}", async (Guid id, ClaimsPrincipal user, VaultDbContext db) =>
{
    var userId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
    var entry = await db.Entries.FirstOrDefaultAsync(e => e.Id == id && e.UserId == userId);
    if (entry is null) return Results.NotFound();
    db.Entries.Remove(entry);
    await db.SaveChangesAsync();
    return Results.Ok();
}).RequireAuthorization();

app.MapFallbackToFile("index.html");

app.Run();

record SignupRequest(
    string Email, string Salt, string LoginAuthProof, string RecoveryAuthProof,
    string WrappedDekByMaster, string WrappedDekByMasterIv,
    string WrappedDekByRecovery, string WrappedDekByRecoveryIv);
record LoginRequest(string Email, string LoginAuthProof);
record RecoverCompleteRequest(
    string Email, string RecoveryAuthProof,
    string NewSalt, string NewLoginAuthProof,
    string NewWrappedDekByMaster, string NewWrappedDekByMasterIv);
record VaultEntryRequest(string Ciphertext, string Iv);

using ITMartinBudget.Application;
using ITMartinBudget.Application.Interfaces;
using ITMartinBudget.Application.Services;
using ITMartinBudget.Infrastructure;
using ITMartinBudget.Infrastructure.Services;
using ITMartinBudget.Server;
using ITMartinBudget.Server.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// =========================
// RAZOR
// =========================
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddSingleton<ToastService>();

// Enum values as strings ("Business" not "1") for the REST endpoints backing
// /shop-categorize's vanilla-JS UI, which compares against the enum's name.
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));

// =========================
// DATABASE
// =========================
var connectionString =
    builder.Environment.IsDevelopment()
        ? builder.Configuration
            .GetConnectionString("BudgetDb")
        : "Data Source=/app/data/budget.db";

Console.WriteLine($"DB: {connectionString}");
builder.Services.AddDbContext<BudgetDbContext>(options =>
{
    options.UseSqlite(connectionString);
});

// =========================
// APPLICATION SERVICES
// =========================
builder.Services.AddScoped<
    IBudgetService,
    BudgetService>();
builder.Services.AddScoped<
    IDashboardService,
    DashboardService>();
builder.Services.AddScoped<
    ITransactionCategorizer,
    TransactionCategorizer>();
builder.Services.AddScoped<
    ITransactionScopeClassifier,
    TransactionScopeClassifier>();
builder.Services.AddScoped<
    ICategoryRuleService,
    CategoryRuleService>();
builder.Services.AddScoped<
    IForwardBudgetService,
    ForwardBudgetService>();
builder.Services.AddScoped<
    ISubscriptionDetectionService,
    SubscriptionDetectionService>();
// =========================
// AI
// =========================
builder.Services.AddScoped<
    IClaudeTransactionCategorizationService,
    ClaudeTransactionCategorizationService>();
builder.Services.AddScoped<
    IInvestigationService,
    InvestigationService>();
builder.Services.AddScoped<
    IFinancialAdvisorService,
    ClaudeFinancialAdvisorService>();
builder.Services.AddScoped<
    ITMartinBudget.Application.Interfaces.ILedgerQaService,
    ITMartinBudget.Infrastructure.Services.LedgerQaService>();
builder.Services.AddScoped<
    IPlannedTransactionService,
    PlannedTransactionService>();
// =========================
// FORECAST
// =========================
builder.Services.AddScoped<
    IFinancialForecastService,
    FinancialForecastService>();
// =========================
// IMPORT / RULES
// =========================
builder.Services.AddScoped<
    BankTransactionCsvService>();
builder.Services.AddScoped<
    ITMartinBudget.Infrastructure.Csv.IBankStatementParser,
    ITMartinBudget.Infrastructure.Csv.RawBankStatementParser>();
builder.Services.AddScoped<
    ITMartinBudget.Infrastructure.Csv.IBankStatementParser,
    ITMartinBudget.Infrastructure.Csv.TotalkontoParser>();
builder.Services.AddScoped<
    LedgerImportService>();
// =========================
// LOGGING
// =========================
builder.Logging.AddFilter(
    "Microsoft.EntityFrameworkCore",
    LogLevel.Warning);

// =========================
// BUILD
// =========================
var app = builder.Build();

// =========================
// DATABASE INIT
// =========================
using (var scope = app.Services.CreateScope())
{
    var db =
        scope.ServiceProvider
            .GetRequiredService<BudgetDbContext>();

    db.Database.Migrate();

    // Demo tier only — set on the demo compose service, never on the real
    // budget-web pointed at the production data volume. Idempotent (see
    // DemoSeeder), so safe even if the container restarts.
    if (app.Configuration.GetValue<bool>("Budget:SeedDemoData"))
        await ITMartinBudget.Infrastructure.DemoSeeder.SeedAsync(db);
}

// =========================
// MIDDLEWARE
// =========================
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseStaticFiles();

var adminPin = app.Configuration["Budget:AdminPin"] ?? "budget2025";
var isDemo = app.Configuration.GetValue<bool>("Budget:SeedDemoData");

// Long random secret for the no-login "quick overview" - deliberately not the
// 4-digit admin PIN, since this link gets shared and saved to a device instead
// of typed. Bootstrapping via /quick/{token} stores it in localStorage; after
// that /quick and /api/quick/* validate it themselves, so both are excluded
// from the PIN-cookie gate below.
var quickToken = app.Configuration["Budget:QuickToken"] ?? "qk-8f2a1c6d9b3e4f0a7c5d2e1b6a9f3c8d";

app.Use(async (ctx, next) =>
{
    var path = ctx.Request.Path.Value ?? "";
    var bypass = path.StartsWith("/_blazor", StringComparison.OrdinalIgnoreCase)
              || path.StartsWith("/_framework", StringComparison.OrdinalIgnoreCase)
              || path.StartsWith("/login", StringComparison.OrdinalIgnoreCase)
              || path.StartsWith("/api/auth", StringComparison.OrdinalIgnoreCase)
              || path.StartsWith("/quick", StringComparison.OrdinalIgnoreCase)
              || path.StartsWith("/api/quick", StringComparison.OrdinalIgnoreCase);
    if (isDemo || bypass || (ctx.Request.Cookies.TryGetValue("budget_auth", out var v) && v == adminPin))
    {
        await next();
        return;
    }
    ctx.Response.Redirect("/login");
});

// Plain multipart form upload (not Blazor InputFile) for the family budget
// page - same Cloudflare Tunnel/SignalR reasoning as the shop upload above.
app.MapPost("/api/budget/upload", async (HttpRequest request, BankTransactionCsvService csvService) =>
{
    var form = await request.ReadFormAsync();
    var file = form.Files["file"];
    if (file is null) return Results.BadRequest("Ingen fil valgt");

    await using var stream = file.OpenReadStream();
    var imported = await csvService.ImportAsync(stream);
    return Results.Ok(new { imported = imported.Count });
}).DisableAntiforgery();

app.MapGet("/api/budget/has-data", async (BudgetDbContext db) =>
    Results.Ok(await db.Transactions.AnyAsync(x => x.LedgerId == "family")));

// Scoped to the family ledger only - a client ledger like "bogshoppen" must
// never be touched by the family page's "delete all data" button.
app.MapPost("/api/budget/reset", async (BudgetDbContext db) =>
{
    var familyTransactions = db.Transactions.Where(x => x.LedgerId == "family");
    db.Transactions.RemoveRange(familyTransactions);
    await db.SaveChangesAsync();
    return Results.Ok();
});

app.MapPost("/api/auth/login", (HttpContext ctx, [Microsoft.AspNetCore.Mvc.FromForm] string pin) =>
{
    if (pin == adminPin)
    {
        ctx.Response.Cookies.Append("budget_auth", adminPin, new CookieOptions
        {
            HttpOnly = true,
            Secure   = true,
            SameSite = SameSiteMode.Strict,
            MaxAge   = TimeSpan.FromDays(30)
        });
        return Results.Redirect("/");
    }
    return Results.Redirect("/login?error=1");
}).DisableAntiforgery();

app.MapGet("/api/auth/logout", (HttpContext ctx) =>
{
    ctx.Response.Cookies.Delete("budget_auth");
    return Results.Redirect("/login");
});

// Imports a shop CSV directly from a path on this machine's disk into a named
// ledger - for Martin's own use running the app locally against a client's
// downloaded bank export, without needing to drive the Blazor upload UI.
// Gated behind the same admin-PIN cookie as everything else (not in the
// bypass list above).
app.MapPost("/api/shop/import-from-path", async (
    string path,
    string ledgerId,
    ITMartinBudget.Infrastructure.Services.LedgerImportService csvService) =>
{
    if (!System.IO.File.Exists(path)) return Results.NotFound($"File not found: {path}");

    await using var stream = System.IO.File.OpenRead(path);
    var imported = await csvService.ImportAsync(stream, ledgerId);
    return Results.Ok(new { imported = imported.Count, ledgerId });
});

// Plain multipart form upload (not Blazor InputFile) so the shop upload page
// can run with no live SignalR circuit at all - see the /shop-categorize
// Cloudflare Tunnel note below for why.
app.MapPost("/api/shop/upload", async (
    HttpRequest request,
    ITMartinBudget.Infrastructure.Services.LedgerImportService csvService,
    ITMartinBudget.Infrastructure.BudgetDbContext db) =>
{
    var form = await request.ReadFormAsync();
    var ledgerId = form["ledgerId"].ToString();
    var file = form.Files["file"];
    if (string.IsNullOrWhiteSpace(ledgerId)) return Results.BadRequest("Angiv et konto-id");
    if (file is null) return Results.BadRequest("Ingen fil valgt");

    // Scope mode is only sent (and only takes effect) the first time this
    // ledger is created - once set on /shop-upload, later uploads to the
    // same ledger reuse the stored choice rather than letting a re-upload
    // silently change it.
    var scopeModeRaw = form["scopeMode"].ToString();
    var existingConfig = await db.LedgerConfigs.FindAsync(ledgerId);
    if (existingConfig is null && Enum.TryParse<ITMartinBudget.Domain.Enums.LedgerScopeMode>(scopeModeRaw, out var scopeMode))
    {
        db.LedgerConfigs.Add(new ITMartinBudget.Domain.Entities.LedgerConfig { LedgerId = ledgerId, ScopeMode = scopeMode });
        await db.SaveChangesAsync();
    }

    await using var stream = file.OpenReadStream();
    var imported = await csvService.ImportAsync(stream, ledgerId);
    return Results.Ok(new { imported = imported.Count, ledgerId });
}).DisableAntiforgery();

app.MapGet("/api/shop/{ledgerId}/scope-mode", async (string ledgerId, ITMartinBudget.Infrastructure.BudgetDbContext db) =>
{
    var config = await db.LedgerConfigs.FindAsync(ledgerId);
    return Results.Ok(new { scopeMode = (config?.ScopeMode ?? ITMartinBudget.Domain.Enums.LedgerScopeMode.Both).ToString() });
});

// Lazy-loaded per-cluster detail for /shop-categorize - only fetched when a
// card is actually expanded, so the main clusters payload doesn't have to
// carry every transaction for every cluster up front (some, like a generic
// "Overførsel" pattern, can have 300+ rows).
app.MapGet("/api/shop/{ledgerId}/transactions", async (string ledgerId, string pattern, ITMartinBudget.Infrastructure.BudgetDbContext db) =>
{
    var transactions = await db.Transactions
        .Where(x => x.LedgerId == ledgerId && x.NormalizedDescription == pattern)
        .OrderByDescending(x => x.Date)
        .Select(x => new { date = x.Date, description = x.Description, amount = x.Amount, rawDetails = x.RawDetails })
        .ToListAsync();
    return Results.Ok(transactions);
});

// Same idea as above but by category name, not pattern - needed on
// /shop-categories since one category can span many patterns (e.g. "Familie"
// covers dozens of different MobilePay-to-a-person patterns).
app.MapGet("/api/shop/{ledgerId}/transactions-by-category", async (string ledgerId, string category, ITMartinBudget.Infrastructure.BudgetDbContext db) =>
{
    var transactions = await db.Transactions
        .Where(x => x.LedgerId == ledgerId && x.UserCategoryName == category)
        .OrderByDescending(x => x.Date)
        .Select(x => new { date = x.Date, description = x.Description, amount = x.Amount, rawDetails = x.RawDetails })
        .ToListAsync();
    return Results.Ok(transactions);
});

// "🔍 Undersøg" button on /shop-categorize - explicitly user-triggered (one
// cluster at a time, or via "Undersøg alle resterende" for everything not
// yet investigated), never automatic on page load, so the AI cost stays
// visible and bounded. Result is cached in TransactionInvestigations so it's
// shown automatically on every later page load without calling Claude again.
app.MapPost("/api/shop/{ledgerId}/investigate", async (
    string ledgerId,
    InvestigateRequest body,
    ITMartinBudget.Application.Interfaces.IInvestigationService investigator,
    ITMartinBudget.Infrastructure.BudgetDbContext db) =>
{
    var result = await investigator.InvestigateAsync(body.Label, body.SampleRawDetails ?? "", body.Count, body.TotalAmount);

    var existing = await db.TransactionInvestigations.FindAsync(ledgerId, body.Pattern);
    if (existing is null)
    {
        existing = new ITMartinBudget.Domain.Entities.TransactionInvestigation { LedgerId = ledgerId, Pattern = body.Pattern };
        db.TransactionInvestigations.Add(existing);
    }
    existing.Reasoning = result.Reasoning;
    existing.SuggestedScope = result.SuggestedScope;
    existing.Confidence = result.Confidence;
    await db.SaveChangesAsync();

    return Results.Ok(result);
});

// "❓ Spørg" box on /shop-overview - free-form questions ("Hvad er
// gennemsnitsindtægten i 2025?") answered from a compact monthly/category
// digest, not a raw transaction dump. Explicitly user-triggered (one click,
// one Haiku call), same cost profile as "Investigate" - never cached since
// each question is different, but each call is cheap.
app.MapPost("/api/shop/{ledgerId}/ask", async (
    string ledgerId,
    AskRequest body,
    ITMartinBudget.Application.Interfaces.ILedgerQaService qa) =>
{
    if (string.IsNullOrWhiteSpace(body.Question))
        return Results.BadRequest("Angiv et spørgsmål.");

    var answer = await qa.AskAsync(ledgerId, body.Question);
    return Results.Ok(new { answer });
});

// REST endpoints backing /shop-categorize's vanilla-JS UI. Deliberately not a
// Blazor InteractiveServer page: Blazor Server needs a persistent SignalR
// circuit, which has previously gotten stuck reconnecting ("Forbinder...")
// through this app's Cloudflare Tunnel domain - same reasoning as
// ITMartinLive/ITMartinStream using REST+polling instead of SignalR.
app.MapGet("/api/shop/{ledgerId}/clusters", async (string ledgerId, ITMartinBudget.Application.Interfaces.ICategoryRuleService rules) =>
    Results.Ok(await rules.GetClustersAsync(ledgerId)));

app.MapGet("/api/shop/{ledgerId}/categories", async (string ledgerId, ITMartinBudget.Application.Interfaces.ICategoryRuleService rules) =>
    Results.Ok(await rules.GetExistingCategoryNamesAsync(ledgerId)));

app.MapPost("/api/shop/{ledgerId}/assign", async (
    string ledgerId,
    ShopAssignRequest body,
    ITMartinBudget.Application.Interfaces.ICategoryRuleService rules) =>
{
    if (string.IsNullOrWhiteSpace(body.CategoryName)) return Results.BadRequest("Angiv en kategori");
    await rules.AssignAsync(ledgerId, body.Pattern, body.CategoryName.Trim(), body.Scope);
    return Results.Ok();
});

// For a pattern that turns out to be a heterogeneous mix (e.g. a generic
// "Overførsel" bank description covering both real Shift4/Flatpay revenue
// AND unrelated personal MobilePay transfers, discovered 2026-07-16) - the
// normal per-pattern rule can't represent two different scopes correctly.
// Splits the pattern's transactions individually by a raw-details keyword
// match, then removes the pattern's single CategoryRule so future imports of
// the same generic description fall back to per-transaction classification
// instead of forcing one scope onto everything again.
app.MapPost("/api/shop/{ledgerId}/split-pattern", async (
    string ledgerId,
    SplitPatternRequest body,
    ITMartinBudget.Infrastructure.BudgetDbContext db) =>
{
    var transactions = await db.Transactions
        .Where(x => x.LedgerId == ledgerId && x.NormalizedDescription == body.Pattern)
        .ToListAsync();

    int matched = 0, unmatched = 0;
    foreach (var tx in transactions)
    {
        if (tx.RawDetails.Contains(body.MatchKeyword, StringComparison.OrdinalIgnoreCase))
        {
            tx.UserCategoryName = body.MatchCategoryName;
            tx.Scope = body.MatchScope;
            matched++;
        }
        else
        {
            tx.UserCategoryName = body.OtherCategoryName;
            tx.Scope = body.OtherScope;
            unmatched++;
        }
    }

    var rule = await db.CategoryRules.FirstOrDefaultAsync(x => x.LedgerId == ledgerId && x.Pattern == body.Pattern);
    if (rule is not null) db.CategoryRules.Remove(rule);

    await db.SaveChangesAsync();
    return Results.Ok(new { matched, unmatched });
});

// For a category that's a mix of a few real people plus some genuinely
// different ones sharing a generic pattern (e.g. "Familie" catching a
// couple of one-off transfers from unrelated people via the old
// "Overførsel" catch-all) - moves just the transactions whose raw bank text
// contains matchKeyword, out of an existing category and into a new one,
// without touching anything else in that category or its CategoryRule.
app.MapPost("/api/shop/{ledgerId}/carve-out", async (
    string ledgerId,
    CarveOutRequest body,
    ITMartinBudget.Infrastructure.BudgetDbContext db) =>
{
    var transactions = await db.Transactions
        .Where(x => x.LedgerId == ledgerId && x.UserCategoryName == body.FromCategory
            && x.RawDetails.Contains(body.MatchKeyword))
        .ToListAsync();

    foreach (var tx in transactions)
    {
        tx.UserCategoryName = body.ToCategory;
        tx.Scope = body.ToScope;
    }

    await db.SaveChangesAsync();
    return Results.Ok(new { moved = transactions.Count });
});

// Backs /shop-categories - the "micro-manage" pass after the initial
// per-pattern categorization is done: merge several small categories
// (Shell, Q8, Uno-X) into one broader one (Benzin).
app.MapGet("/api/shop/{ledgerId}/category-summary", async (string ledgerId, ITMartinBudget.Application.Interfaces.ICategoryRuleService rules) =>
    Results.Ok(await rules.GetCategorySummaryAsync(ledgerId)));

app.MapPost("/api/shop/{ledgerId}/merge-categories", async (
    string ledgerId,
    MergeCategoriesRequest body,
    ITMartinBudget.Application.Interfaces.ICategoryRuleService rules) =>
{
    if (body.SourceNames.Count == 0) return Results.BadRequest("Vælg mindst én kategori");
    if (string.IsNullOrWhiteSpace(body.TargetName)) return Results.BadRequest("Angiv navnet på den samlede kategori");
    await rules.MergeCategoriesAsync(ledgerId, body.SourceNames, body.TargetName.Trim());
    return Results.Ok();
});

// "Skift til Forretning/Privat" button on /shop-categories - lets a whole
// category's scope be flipped in one click (e.g. a MobilePay-per-person
// category that turns out to be a customer paying for a purchase, not a
// private transfer) instead of redoing it pattern by pattern.
app.MapPost("/api/shop/{ledgerId}/set-category-scope", async (
    string ledgerId,
    SetCategoryScopeRequest body,
    ITMartinBudget.Application.Interfaces.ICategoryRuleService rules) =>
{
    await rules.SetCategoryScopeAsync(ledgerId, body.CategoryName, body.Scope);
    return Results.Ok();
});

// One Claude call for the whole category list - suggests groups to merge,
// so the user isn't manually hunting through 50+ names for related ones.
// Purely suggestions; nothing is merged until reviewed and confirmed.
app.MapPost("/api/shop/{ledgerId}/suggest-merges", async (
    string ledgerId,
    ITMartinBudget.Application.Interfaces.ICategoryRuleService rules,
    ITMartinBudget.Application.Interfaces.IInvestigationService investigator) =>
{
    var categories = await rules.GetCategorySummaryAsync(ledgerId);
    var suggestions = await investigator.SuggestMergesAsync(
        categories.Select(c => (c.Name, c.Count, c.Sum)).ToList());
    return Results.Ok(suggestions);
});

app.MapPost("/api/shop/{ledgerId}/assign-batch", async (
    string ledgerId,
    List<ShopAssignRequest> body,
    ITMartinBudget.Application.Interfaces.ICategoryRuleService rules) =>
{
    foreach (var item in body)
    {
        if (string.IsNullOrWhiteSpace(item.CategoryName)) continue;
        await rules.AssignAsync(ledgerId, item.Pattern, item.CategoryName.Trim(), item.Scope);
    }
    return Results.Ok(new { saved = body.Count });
});

// Re-runs the scope classifier against every transaction in the ledger that
// hasn't been manually categorized yet (UserCategoryName is null) - lets a
// classifier rule fix (e.g. the "priv" marker added 2026-07-16) correct
// already-imported rows without touching anything the user already reviewed.
app.MapPost("/api/shop/{ledgerId}/reclassify", async (
    string ledgerId,
    ITMartinBudget.Infrastructure.BudgetDbContext db,
    ITMartinBudget.Application.Interfaces.ITransactionScopeClassifier classifier) =>
{
    var transactions = await db.Transactions
        .Where(x => x.LedgerId == ledgerId && x.UserCategoryName == null)
        .ToListAsync();

    foreach (var tx in transactions)
        classifier.Classify(tx);

    await db.SaveChangesAsync();
    return Results.Ok(new { reclassified = transactions.Count });
});

app.MapGet("/api/quick/overview", async (string? token, IDashboardService dashboardService) =>
{
    if (token != quickToken) return Results.Unauthorized();

    var dashboard = await dashboardService.BuildDashboardAsync();
    var now = DateTime.Now;
    var currentMonthTx = dashboard.Transactions
        .Where(x => x.Date.Month == now.Month && x.Date.Year == now.Year)
        .ToList();

    var income = currentMonthTx.Where(x => x.Amount > 0).Sum(x => x.Amount);
    var expenses = Math.Abs(currentMonthTx.Where(x => x.Amount < 0).Sum(x => x.Amount));

    return Results.Ok(new
    {
        month = now.ToString("MMMM yyyy", new System.Globalization.CultureInfo("da-DK")),
        income,
        expenses,
        net = income - expenses
    });
});

app.MapGet("/api/quick/subscriptions", async (string? token, ISubscriptionDetectionService subscriptionService) =>
{
    if (token != quickToken) return Results.Unauthorized();

    var list = await subscriptionService.DetectAsync();
    return Results.Ok(list.Select(s => new
    {
        s.Amount,
        s.IntervalLabel,
        s.Occurrences,
        s.LastChargedDate,
        s.DaysSinceLastCharge,
        s.SampleDescription
    }));
});

app.UseAntiforgery();

// =========================
// BLAZOR
// =========================
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// =========================
// RUN
// =========================
app.Run();

record ShopAssignRequest(string Pattern, string CategoryName, ITMartinBudget.Domain.Enums.TransactionScope Scope);
record InvestigateRequest(string Pattern, string Label, string? SampleRawDetails, int Count, decimal TotalAmount);
record MergeCategoriesRequest(List<string> SourceNames, string TargetName);
record SetCategoryScopeRequest(string CategoryName, ITMartinBudget.Domain.Enums.TransactionScope Scope);
record SplitPatternRequest(
    string Pattern,
    string MatchKeyword,
    string MatchCategoryName,
    ITMartinBudget.Domain.Enums.TransactionScope MatchScope,
    string OtherCategoryName,
    ITMartinBudget.Domain.Enums.TransactionScope OtherScope);
record CarveOutRequest(
    string FromCategory,
    string MatchKeyword,
    string ToCategory,
    ITMartinBudget.Domain.Enums.TransactionScope ToScope);
record AskRequest(string Question);
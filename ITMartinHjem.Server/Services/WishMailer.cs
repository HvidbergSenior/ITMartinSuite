using ITMartinHjem.Application;

namespace ITMartinHjem.Server.Services;

/// <summary>Once a day at 9: mails each customer the things that turned up for their "Mine ønsker" (user 2026-11-21:
/// "notifications on special wares that customer wants"). Only while the customer market is on (Hjem__MarketForCustomers) -
/// nothing is mailed about a shop people cannot log in to. Each thing is mailed once (WishMailed).</summary>
public sealed class WishMailer(IServiceScopeFactory scopes, ShopVisibility visibility, IConfiguration cfg, ILogger<WishMailer> log) : BackgroundService
{
    private static readonly TimeZoneInfo Copenhagen = TimeZoneInfo.FindSystemTimeZoneById("Europe/Copenhagen");
    public const int Hour = 9;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (!visibility.Customers) { log.LogInformation("Wish mail off (market for customers is off)"); return; }
        var shopUrl = cfg["Hjem:BogshoppenUrl"] ?? "https://bogshoppen.dk";
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(10));
        DateOnly? done = null;
        do
        {
            var now = TimeZoneInfo.ConvertTime(DateTime.UtcNow, Copenhagen);
            if (now.Hour < Hour || done == DateOnly.FromDateTime(now)) continue;
            try
            {
                using var scope = scopes.CreateScope();
                var wishes = scope.ServiceProvider.GetRequiredService<WishUseCases>();
                var notifier = scope.ServiceProvider.GetRequiredService<ICustomerNotifier>();
                var sent = await wishes.MailFoundAsync(notifier, shopUrl, ct);
                log.LogInformation("Wish mail: {Sent} customers mailed", sent);
                done = DateOnly.FromDateTime(now);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested) { log.LogWarning(ex, "Wish mail failed - tries again in 10 min"); }
        } while (await timer.WaitForNextTickAsync(ct));
    }
}

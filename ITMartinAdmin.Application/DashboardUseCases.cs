using ITMartinAdmin.Contracts;
using ITMartinAdmin.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace ITMartinAdmin.Application;

/// <summary>One app that answers the status call. Throws when it does not answer (down, stopped, wrong key).</summary>
public interface IStatusSource
{
    string App { get; }
    Task<List<StatusLine>> LinesAsync(CancellationToken ct);
}

/// <summary>The front page: the buttons, and every app's numbers - asked at the same time, each with its own short
/// wait, so one app that is stopped never holds the page.</summary>
public sealed class DashboardUseCases(IEnumerable<IStatusSource> sources)
{
    public static readonly TimeSpan Wait = TimeSpan.FromSeconds(4);

    public IEnumerable<IGrouping<string, AdminLink>> Links => AdminLinks.Grouped();

    public async Task<List<AppAnswer>> AnswersAsync(CancellationToken ct = default) =>
        (await Task.WhenAll(sources.Select(s => AskAsync(s, ct)))).ToList();

    private static async Task<AppAnswer> AskAsync(IStatusSource source, CancellationToken ct)
    {
        using var wait = CancellationTokenSource.CreateLinkedTokenSource(ct);
        wait.CancelAfter(Wait);
        try { return new AppAnswer(source.App, await source.LinesAsync(wait.Token), true); }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested) { return AppAnswer.Down(source.App); }
    }
}

public static class DependencyInjection
{
    public static IServiceCollection AddAdminApplication(this IServiceCollection services)
    {
        services.AddScoped<DashboardUseCases>();
        return services;
    }
}

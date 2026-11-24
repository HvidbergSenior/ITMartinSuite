using ITMartinElPriser.Domain;

namespace ITMartinElPriser.Application;

/// <summary>Sends one plain-text mail. Without SMTP settings nothing is sent and Configured is false.</summary>
public interface IMailSender
{
    bool Configured { get; }
    Task<bool> SendAsync(string to, string subject, string body, CancellationToken ct = default);
}

/// <summary>The account store as the reset flow needs it (MinElpris implements it on its own database).</summary>
public interface IResetAccounts
{
    /// <summary>The account's display name, or null when no account has this mail.</summary>
    Task<string?> FindNameAsync(string email);
    /// <summary>When the account last got a reset ticket (null = never).</summary>
    Task<DateTime?> LastTicketUtcAsync(string email);
    Task SaveTicketAsync(string email, string tokenHash, DateTime createdUtc, DateTime expiresUtc);
    /// <summary>Sets the new password if the ticket is usable, marks it used and drops the account's other tickets.</summary>
    Task<bool> UseTicketAsync(string tokenHash, string newPassword, DateTime nowUtc);
    Task<bool> TicketUsableAsync(string tokenHash, DateTime nowUtc);
}

public enum ResetRequest { Sent, MailOff }

public sealed record ResetDone(bool Ok, string? Error = null);

/// <summary>Forgotten password by mail. The answer to a request never says whether the mail has an account.</summary>
public sealed class PasswordResetUseCases(IResetAccounts accounts, IMailSender mail, Func<DateTime>? clock = null)
{
    private DateTime Now => (clock ?? (() => DateTime.UtcNow))();

    public bool MailWorks => mail.Configured;

    /// <param name="resetUrl">The page the link opens, e.g. https://minelpris.itmartin.dk/nulstil - the token is added.</param>
    public async Task<ResetRequest> RequestAsync(string email, string resetUrl, CancellationToken ct = default)
    {
        if (!mail.Configured) return ResetRequest.MailOff;
        email = (email ?? "").Trim().ToLowerInvariant();
        if (email.Length == 0 || await accounts.FindNameAsync(email) is not { } name) return ResetRequest.Sent;
        if (await accounts.LastTicketUtcAsync(email) is { } last && Now - last < PasswordResetTicket.MinGap) return ResetRequest.Sent;

        var ticket = PasswordResetTicket.Create(Now);
        await accounts.SaveTicketAsync(email, ticket.TokenHash, ticket.CreatedUtc, ticket.ExpiresUtc);
        var link = $"{resetUrl}?t={ticket.Token}";
        var hello = string.IsNullOrWhiteSpace(name) ? "Hej" : $"Hej {name.Trim()}";
        await mail.SendAsync(email, "Ny adgangskode til MinElpris", $"""
            {hello}

            Du (eller nogen med din mail) har bedt om en ny adgangskode til MinElpris.

            Sådan gør du:
            1. Tryk på linket herunder – det virker i 1 time og kun én gang.
            2. Skriv din nye adgangskode (mindst 8 tegn) to gange.
            3. Log ind med den nye adgangskode.

            {link}

            Har du ikke bedt om det? Så gør du ingenting – din adgangskode er ikke ændret.

            Hilsen Martin Hvidberg, ITMartin
            ITMartin@Mensa.dk · 31 19 47 30
            """, ct);
        return ResetRequest.Sent;
    }

    public Task<bool> LinkWorksAsync(string token) =>
        string.IsNullOrEmpty(token) ? Task.FromResult(false) : accounts.TicketUsableAsync(PasswordResetTicket.Hash(token), Now);

    public async Task<ResetDone> CompleteAsync(string token, string password, string repeat, int minLength)
    {
        if ((password ?? "").Length < minLength) return new(false, $"Adgangskoden skal være mindst {minLength} tegn.");
        if (password != repeat) return new(false, "De to adgangskoder er ikke ens. Skriv den samme to gange.");
        if (string.IsNullOrEmpty(token) || !await accounts.UseTicketAsync(PasswordResetTicket.Hash(token), password!, Now))
            return new(false, "Linket virker ikke længere (det holder 1 time og kan kun bruges én gang). Bed om et nyt.");
        return new(true);
    }
}

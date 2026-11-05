using System.Text.Json;
using ITMartinIndbakke.Application;
using ITMartinIndbakke.Domain;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace ITMartinIndbakke.Infrastructure;

/// <summary>
/// Unsubscribe without visiting a page: RFC 8058 one-click (POST "List-Unsubscribe=One-Click" to the https address),
/// or a short mail to the list's mailto address, sent from the user's own Gmail (same app password, smtp.gmail.com).
/// </summary>
public sealed class Unsubscriber(HttpClient http, string user, string appPassword,
    Func<SmtpClient, CancellationToken, Task>? login = null, string smtpHost = "smtp.gmail.com", int smtpPort = 465,
    SecureSocketOptions smtpSecurity = SecureSocketOptions.SslOnConnect) : IUnsubscriber
{
    public async Task<bool> UnsubscribeAsync(UnsubscribeTarget target, CancellationToken ct)
    {
        if (target.Kind == UnsubscribeKind.OneClick && target.Https is { } url)
        {
            try
            {
                using var body = new FormUrlEncodedContent([new("List-Unsubscribe", "One-Click")]);
                using var r = await http.PostAsync(url, body, ct);
                if (r.IsSuccessStatusCode) return true;
            }
            catch (HttpRequestException) { }
            catch (TaskCanceledException) when (!ct.IsCancellationRequested) { }
            if (target.MailTo is null) return false;   // fall back to the mail below when the list also gave one
        }

        if (target.MailTo is not { } to) return false;
        var msg = new MimeMessage();
        msg.From.Add(MailboxAddress.Parse(user));
        msg.To.Add(MailboxAddress.Parse(to));
        msg.Subject = string.IsNullOrWhiteSpace(target.MailSubject) ? "unsubscribe" : target.MailSubject;
        msg.Body = new TextPart("plain") { Text = "unsubscribe" };
        using var smtp = new SmtpClient { Timeout = 30_000 };
        await smtp.ConnectAsync(smtpHost, smtpPort, smtpSecurity, ct);
        if (login is not null) await login(smtp, ct);   // Outlook: Microsoft sign-in (OAuth)
        else await smtp.AuthenticateAsync(user, appPassword.Replace(" ", ""), ct);
        await smtp.SendAsync(msg, ct);
        await smtp.DisconnectAsync(true, ct);
        return true;
    }
}

/// <summary>The presses as one JSON file (/data/oprydning.json): time, sender, Message-IDs, what was unsubscribed. Nothing of the mails.</summary>
public sealed class JsonCleanupLog(string path) : ICleanupLog
{
    private readonly object _gate = new();
    private List<CleanupAction>? _items;

    public IReadOnlyList<CleanupAction> All()
    {
        lock (_gate) return Load().ToList();
    }

    public void Save(CleanupAction action)
    {
        lock (_gate)
        {
            var items = Load();
            items.RemoveAll(a => a.Id == action.Id);
            items.Add(action);
            items.RemoveAll(a => a.At < DateTimeOffset.UtcNow.AddDays(-90));   // Papirkurv is empty after 30 days anyway
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            var tmp = path + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(items));
            File.Move(tmp, path, overwrite: true);
        }
    }

    private List<CleanupAction> Load() =>
        _items ??= File.Exists(path) ? JsonSerializer.Deserialize<List<CleanupAction>>(File.ReadAllText(path)) ?? [] : [];
}

using System.Net;
using System.Net.Mail;
using ITMartinElPriser.Application;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ITMartinElPriser.Infrastructure;

// MinElpris mail (password reset, 2026-11-24): FROM martinhvidbergnielsen@gmail.com through smtp.gmail.com (user's choice
// 2026-11-24). A Gmail address sent by Gmail passes DMARC; ITMartin@mensa.dk could not be used (mensa.dk is Mensa
// Danmark's domain, DMARC p=reject, its ImprovMX SMTP login is not Martin's to hand out).
// magic.env: MitEl__Smtp__Password = a Google APP PASSWORD for that account (myaccount.google.com/apppasswords, needs
// 2-step verification). Host/User/Port default to smtp.gmail.com / martinhvidbergnielsen@gmail.com / 587. Without the
// password nothing is sent and the page tells the visitor to write or call Martin.
public sealed class SmtpMailSender(IConfiguration cfg, ILogger<SmtpMailSender> log) : IMailSender
{
    private string Host => cfg["MitEl:Smtp:Host"] is { Length: > 0 } h ? h : "smtp.gmail.com";
    private int Port => int.TryParse(cfg["MitEl:Smtp:Port"], out var p) ? p : 587;
    private string User => cfg["MitEl:Smtp:User"] is { Length: > 0 } u ? u : "martinhvidbergnielsen@gmail.com";
    private string? Password => cfg["MitEl:Smtp:Password"];
    private string From => cfg["MitEl:Smtp:From"] is { Length: > 0 } f ? f : User;

    public bool Configured => !string.IsNullOrWhiteSpace(Password);

    public async Task<bool> SendAsync(string to, string subject, string body, CancellationToken ct = default)
    {
        if (!Configured) { log.LogWarning("Mail not sent: MitEl__Smtp__Password (Gmail app password) is not set"); return false; }
        try
        {
            using var msg = new MailMessage(new MailAddress(From, "MinElpris · ITMartin"), new MailAddress(to))
            {
                Subject = subject, Body = body,
                BodyEncoding = System.Text.Encoding.UTF8, SubjectEncoding = System.Text.Encoding.UTF8,
            };
            using var smtp = new SmtpClient(Host, Port) { EnableSsl = true, Credentials = new NetworkCredential(User, Password) };
            await smtp.SendMailAsync(msg, ct);
            log.LogInformation("Mail sent: {Subject}", subject);
            return true;
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "Mail failed: {Subject}", subject);
            return false;
        }
    }
}

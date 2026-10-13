using System.Net;
using System.Net.Mail;
using ITMartinClub.Application;
using ITMartinClub.Domain;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ITMartinClub.Infrastructure.External;

// Mail copy of team messages and invitations. Gmail since 2026-10-02 (Club__Smtp__Host/User/Password in magic.env). Without it nothing is mailed - the app + push still work and the page says so.
internal sealed class SmtpMailSender(IConfiguration cfg, ILogger<SmtpMailSender> log) : IMailSender
{
    private string Host => cfg["Club:Smtp:Host"] ?? "send.one.com";
    private int Port => cfg.GetValue("Club:Smtp:Port", 587);
    private string User => cfg["Club:Smtp:User"] ?? "info@itmartin.dk";
    private string? Password => cfg["Club:Smtp:Password"];

    public bool Configured => !string.IsNullOrWhiteSpace(Password);

    public async Task<bool> SendAsync(string to, string fromName, string subject, string body)
    {
        if (!Configured || !EmailAddress.LooksLike(to)) return false;
        try
        {
            using var msg = new MailMessage(new MailAddress(User, $"{fromName} via Club"), new MailAddress(to))
            {
                Subject = subject, Body = body, BodyEncoding = System.Text.Encoding.UTF8, SubjectEncoding = System.Text.Encoding.UTF8,
            };
            using var smtp = new SmtpClient(Host, Port) { EnableSsl = true, Credentials = new NetworkCredential(User, Password) };
            await smtp.SendMailAsync(msg);
            return true;
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "Club mail failed");
            return false;
        }
    }
}

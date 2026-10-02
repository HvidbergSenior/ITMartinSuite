using System.Net;
using System.Net.Mail;

namespace ITMartinClub.Server.Services;

// Mail copy of team messages. Same one.com mailbox as itmartin.dk (info@itmartin.dk); password in magic.env as
// Club__Smtp__Password. Without it nothing is mailed - the app + push still work and the page says so.
public sealed class ClubMailService(IConfiguration cfg, ILogger<ClubMailService> log)
{
    private string Host => cfg["Club:Smtp:Host"] ?? "send.one.com";
    private int Port => cfg.GetValue("Club:Smtp:Port", 587);
    private string User => cfg["Club:Smtp:User"] ?? "info@itmartin.dk";
    private string? Password => cfg["Club:Smtp:Password"];

    public bool Configured => !string.IsNullOrWhiteSpace(Password);

    public async Task<bool> SendAsync(string to, string fromName, string subject, string body)
    {
        if (!Configured || !LooksLikeEmail(to)) return false;
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

    public static bool LooksLikeEmail(string? s) =>
        s is { Length: > 5 and < 200 } && System.Text.RegularExpressions.Regex.IsMatch(s, @"^[^@\s]+@[^@\s]+\.[^@\s]+$");
}

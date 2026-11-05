namespace ITMartinIndbakke.Domain;

// Min Indbakke (user 2026-11-05): the paid version - the person types their customer number + their own Gmail app password
// EVERY time they log in ("they provide it each time they login"). The key is never written to disk; it lives in memory
// while the page is used and is forgotten after IdleTimeout. Martin decides who may use it (paid on request).
public static class MinIndbakkeRules
{
    /// <summary>Quiet this long = logged out, and the key is gone from memory.</summary>
    public static readonly TimeSpan IdleTimeout = TimeSpan.FromMinutes(30);

    /// <summary>The 8 digits of a kundenummer, whatever the person typed around them ("1234 5678", "1234-5678").</summary>
    public static string NormalizeCode(string? code) => new((code ?? "").Where(char.IsDigit).ToArray());

    /// <returns>A Danish line for the person, or null when the three fields look right.</returns>
    public static string? CheckInput(string code, string email, string appPassword)
    {
        if (code.Length != 8) return "Dit nummer har 8 cifre – du finder det i mailen fra Martin.";
        email = email.Trim();
        if (email.Length < 6 || !email.Contains('@') || email.Contains(' ')) return "Skriv din Gmail-adresse, fx navn@gmail.com.";
        var key = appPassword.Replace(" ", "");
        if (key.Length != 16 || !key.All(char.IsLetter)) return "Nøglen er 16 bogstaver – du laver den hos Google (tryk Vis mig hvordan).";
        return null;
    }

    /// <summary>Folder name for the person's own marks (Klaret, frister) - digits only, so it can never point elsewhere.</summary>
    public static string AccountKey(string code) => "kunde-" + NormalizeCode(code);
}

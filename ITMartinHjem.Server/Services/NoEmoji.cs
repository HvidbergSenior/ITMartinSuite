using System.Text.RegularExpressions;

namespace ITMartinHjem.Server.Services;

// No emojis anywhere on itmartin.dk or bogshoppen.dk (user 2026-10-12: they look AI-made - "personal, rather pictures").
// Text from Svar (pages, Q&A, posts) is shown through Strip, so an emoji typed later does not show either.
// Arrows, ✓ (U+2713) and ✕ (U+2715) are plain typography and stay.
public static class NoEmoji
{
    private static readonly Regex Keycap = new(@"([0-9])️?⃣ ?", RegexOptions.Compiled);
    private static readonly Regex Emoji = new(
        @"(?:[☀-⛿✀-✒✔✖-➿⭐⭕⏩-⏺⌚⌛]|\uD83C[\uDC00-\uDFFF]|\uD83D[\uDC00-\uDFFF]|\uD83E[\uDC00-\uDEFF])️?‍?",
        RegexOptions.Compiled);
    private static readonly Regex Spaces = new(@"[ \t]{2,}", RegexOptions.Compiled);
    private static readonly Regex LineStart = new(@"(?m)^(\*|#{2,3}|-)? +", RegexOptions.Compiled);

    public static string Strip(string? text)
    {
        if (string.IsNullOrEmpty(text)) return text ?? "";
        var t = Keycap.Replace(text, "$1. ");
        t = Emoji.Replace(t, "").Replace("️", "");
        t = Spaces.Replace(t, " ");
        // "## 👋 Hvem" -> "## Hvem", "* 📱 **Gratis" -> "* **Gratis", " Tekst" at a line start -> "Tekst"
        t = LineStart.Replace(t, m => m.Groups[1].Success ? m.Groups[1].Value + " " : "");
        return t.Trim();
    }
}

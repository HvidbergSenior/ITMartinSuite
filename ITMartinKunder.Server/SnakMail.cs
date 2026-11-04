namespace ITMartinKunder.Server;

/// <summary>"Send linket på mail": Martin's own mail program opens with the room link written (he presses Send himself).</summary>
public static class SnakMail
{
    public static string MailTo(Customer c)
    {
        var hej = c.Name == "" ? "Hej" : "Hej " + c.Name.Split(' ')[0];
        var body = hej + "\n\nHer er dit eget snak-rum, hvor vi kan skrive sammen:\n" + c.SnakLink + "\n\n"
            + "Tryk på linket og skriv dit navn, så er du inde. Skriv gerne der i stedet for på mail, så ser vi begge alt med det samme."
            + "\n\nVenlig hilsen\nMartin";
        return "mailto:" + c.Email + "?subject=" + Uri.EscapeDataString("Dit snak-rum hos ITMartin") + "&body=" + Uri.EscapeDataString(body);
    }
}

namespace ITMartin.Magic.Infrastructure.OCR;

public static class SetCodeDictionary
{
    private static readonly Dictionary<string, string>
        Map =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["LEA"] = "lea",
                ["LEB"] = "leb",
                ["2ED"] = "2ed",
                ["3ED"] = "3ed",
                ["4ED"] = "4ed",
                ["5ED"] = "5ed",

                ["DMR"] = "dmr",
                ["BRO"] = "bro",
                ["ONE"] = "one",
                ["MKM"] = "mkm",
                ["OTJ"] = "otj",
                ["MH3"] = "mh3",
                ["BLB"] = "blb",

                ["NEO"] = "neo",
                ["KHM"] = "khm",
                ["MID"] = "mid",
                ["VOW"] = "vow",

                ["RTR"] = "rtr",
                ["GRN"] = "grn",
                ["RNA"] = "rna"
            };

    public static string? Normalize(
        string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        text =
            text
                .Trim()
                .ToUpperInvariant();

        return Map.GetValueOrDefault(text);
    }
}
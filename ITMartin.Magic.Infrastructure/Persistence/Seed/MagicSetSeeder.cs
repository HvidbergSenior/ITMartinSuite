public static class MagicSetSeeder
{
    public static IEnumerable<MagicSetKnowledge> GetSets()
    {
        yield return new MagicSetKnowledge
        {
            SetCode = "lea",
            SetName = "Limited Edition Alpha",
            SymbolDescription = "No set symbol",
            SymbolKeywords = "none",
            ReleaseYear = 1993,
            UsesOldFrame = true,
            UsesWhiteBorder = false,
            UsesBlackBorder = true,
            HasCollectorNumbers = false,
            HasFoils = false,
            HasSetSymbol = false
        };

        yield return new MagicSetKnowledge
        {
            SetCode = "2ed",
            SetName = "Unlimited Edition",
            SymbolDescription = "No set symbol",
            SymbolKeywords = "none",
            ReleaseYear = 1993,
            UsesOldFrame = true,
            UsesWhiteBorder = true,
            UsesBlackBorder = false,
            HasCollectorNumbers = false,
            HasFoils = false,
            HasSetSymbol = false
        };

        yield return new MagicSetKnowledge
        {
            SetCode = "3ed",
            SetName = "Revised Edition",
            SymbolDescription = "No set symbol",
            SymbolKeywords = "none",
            ReleaseYear = 1994,
            UsesOldFrame = true,
            UsesWhiteBorder = true,
            UsesBlackBorder = false,
            HasCollectorNumbers = false,
            HasFoils = false,
            HasSetSymbol = false
        };
    }
}
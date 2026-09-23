namespace ITMartinR6Assistant.Domain;

// One played round in the current match: which side we were on, the site in play and whether we won.
// Kept in the live session only; at "Kampen er slut" the rounds fill in the match log.
public sealed record RoundResult(int Number, string Side, string? Site, bool Won);

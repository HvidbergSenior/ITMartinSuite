namespace ITMartinR6Assistant.Domain;

// The stages a round goes through, in order. Advanced manually by whoever's
// running the session (no auto-advance on timer expiry - the countdown is
// informational, not a forcing function).
public enum MatchPhase
{
    Lobby,
    MapBans,
    OperatorBans,
    OperatorPick,
    InGame,
    PostMatch,
}

namespace ITMartinSnak.Application;

/// <summary>Who caused a push: the phone it came from, the name they write as, and whether it is Martin (the host).
/// Their own phones get no push (PushRules.Targets).</summary>
public sealed record PushFrom(string? Endpoint, string? Name = null, bool Host = false);

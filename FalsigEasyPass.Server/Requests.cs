namespace FalsigEasyPass.Server;

/// <summary>POST /api/steder/{id}/besked: Ejer = "Det er vores sted" (mail required), else "Er noget forkert?".</summary>
public sealed record BeskedRequest(string? Tekst, string? Mail, bool Ejer, bool Godt = false);

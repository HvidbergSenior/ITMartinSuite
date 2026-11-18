namespace EasyPass.Server;

/// <summary>POST /api/steder/{id}/besked: Ejer = "Det er vores sted" (mail required), else "Er noget forkert?".</summary>
public sealed record PlanlaegRequest(string? Behov, string? Dato, string? MitNavn, string? Hjaelper);

public sealed record BeskedRequest(string? Tekst, string? Mail, bool Ejer, bool Godt = false);

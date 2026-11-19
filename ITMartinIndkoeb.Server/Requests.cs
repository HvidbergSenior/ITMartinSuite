namespace ITMartinIndkoeb.Server;

public sealed record OpretRequest(string? Navn);
public sealed record KoebRequest(decimal Beloeb, string? Butik, string? Hvem, DateOnly? Dato);
public sealed record MaalRequest(decimal UgeMaal, int MaxTure, decimal SupplGraense, decimal StortFra, string? Navn);
public sealed record VareRequest(string? Tekst, string? Hvem, string? Butik, string? Tilbud);
public sealed record AfkrydsRequest(bool Koebt);
public sealed record TurRequest(string? Butik, string? Hvem, DateOnly? Dag = null);
public sealed record StedRequest(string? Postnr);
public sealed record SkjulRequest(string? Butik, bool Skjul);
public sealed record AppRequest(string? App, bool Har);

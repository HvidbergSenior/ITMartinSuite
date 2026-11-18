namespace ITMartinIndkoeb.Server;

public sealed record OpretRequest(string? Navn);
public sealed record KoebRequest(decimal Beloeb, string? Butik, string? Hvem, DateOnly? Dato);
public sealed record MaalRequest(decimal UgeMaal, int MaxTure, decimal SupplGraense, decimal StortFra, string? Navn);

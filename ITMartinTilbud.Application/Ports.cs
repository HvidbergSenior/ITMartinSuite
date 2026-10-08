using ITMartinTilbud.Domain;

namespace ITMartinTilbud.Application;

/// <summary>The leaflet service (Tjek / eTilbudsavis in production).</summary>
public interface IOfferSource
{
    /// <summary>Grocery offers for one search term near a place, already read into offers (unit prices, app prices).</summary>
    Task<List<Offer>> SearchAsync(string term, double lat, double lng, int km, CancellationToken ct);

    /// <summary>The nearest store of a chain, or null when unknown.</summary>
    Task<NearestStore?> NearestStoreAsync(string dealerId, double lat, double lng, CancellationToken ct);

    Task<List<Leaflet>> LeafletsAsync(double lat, double lng, CancellationToken ct);

    Task<List<Offer>> LeafletOffersAsync(Leaflet leaflet, CancellationToken ct);
}

/// <summary>Salling's markdowns near their date. Throws <see cref="FoodWasteQuotaException"/> when the day's calls are spent.</summary>
public interface IFoodWasteSource
{
    bool Enabled { get; }
    Task<List<Clearance>> NearAsync(double lat, double lng, int km, CancellationToken ct);
}

public interface IFixedDealStore
{
    IReadOnlyList<FixedDeal> All();
    void Add(FixedDeal deal);
    bool Remove(string id);
}

public interface IPlaceLookup
{
    Task<Place?> FromPostcodeAsync(string postcode, CancellationToken ct);
}

public sealed record Place(string Name, double Lat, double Lng);

/// <summary>An outside service did not answer.</summary>
public sealed class SourceUnavailableException(string message) : TilbudException(message);

public sealed class FoodWasteQuotaException()
    : TilbudException("Madspild er brugt op for i dag (Salling giver 100 opslag om dagen). Prøv igen i morgen.");

// =========================
// FAKE GPS SERVICE
// =========================

using ITMartinFileSorter.Domain.Interfaces;

namespace ITMartinFileSorter.Tests.Services;

public class FakeGpsService : IGpsService
{
    public (double lat, double lng)? GetCoordinates(string path) => null;
}

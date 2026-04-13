using ITMartinLibrary.Domain.Entities;

namespace ITMartinLibrary.Application.Interfaces;

public interface IBarcodeLookupService
{
    Task<InventoryItem?> LookupAsync(string barcode);
}
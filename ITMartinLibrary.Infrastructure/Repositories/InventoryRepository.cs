using ITMartinLibrary.Application.Interfaces;
using ITMartinLibrary.Domain;
using ITMartinLibrary.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ITMartinLibrary.Infrastructure.Repositories
{
    public class InventoryRepository : IInventoryRepository
    {
        private readonly LibraryDbContext _db;

        public InventoryRepository(LibraryDbContext db)
        {
            _db = db;
        }

        public async Task<List<InventoryItem>> GetAllAsync()
        {
            return await _db.Items.ToListAsync();
        }

        public async Task AddAsync(InventoryItem item)
        {
            _db.Items.Add(item);
            await _db.SaveChangesAsync();
        }

        public async Task<List<InventoryItem>> SearchAsync(string text)
        {
            return await _db.Items
                .Where(x =>
                    x.Title.Contains(text) ||
                    x.Barcode.Contains(text))
                .ToListAsync();
        }
        
        public async Task UpdateAsync(InventoryItem item)
        {
            _db.Items.Update(item);
            await _db.SaveChangesAsync();
        }
        public async Task<InventoryItem?> GetByBarcodeAsync(string barcode)
        {
            return await _db.Items
                .FirstOrDefaultAsync(x => x.Barcode == barcode);
        }
    }
}
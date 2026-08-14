using ITMartinLibrary.Application.Interfaces;
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

        public async Task<List<InventoryItem>> GetAllAsync(Guid groupId)
        {
            return await _db.Items.Where(x => x.GroupId == groupId).ToListAsync();
        }

        public async Task AddAsync(InventoryItem item)
        {
            _db.Items.Add(item);
            await _db.SaveChangesAsync();
        }

        public async Task<List<InventoryItem>> SearchAsync(Guid groupId, string text)
        {
            return await _db.Items
                .Where(x => x.GroupId == groupId &&
                    (x.Title.Contains(text) || x.Barcode.Contains(text)))
                .ToListAsync();
        }

        public async Task<InventoryItem?> GetByBarcodeAsync(Guid groupId, string barcode)
        {
            return await _db.Items
                .FirstOrDefaultAsync(x => x.GroupId == groupId && x.Barcode == barcode);
        }

        public async Task<InventoryItem?> GetByTitleAsync(Guid groupId, string title)
        {
            return await _db.Items
                .FirstOrDefaultAsync(x => x.GroupId == groupId && x.Title.ToLower() == title.ToLower());
        }

        // Filters by GroupId too (not just Id) so a URL with someone else's
        // item id can't be used to read/edit across tenants.
        public async Task<InventoryItem?> GetByIdAsync(Guid groupId, int id)
        {
            return await _db.Items.FirstOrDefaultAsync(x => x.Id == id && x.GroupId == groupId);
        }

        public async Task UpdateAsync(InventoryItem item)
        {
            _db.Items.Update(item);
            await _db.SaveChangesAsync();
        }
    }
}
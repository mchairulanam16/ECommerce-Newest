using ECommerce.Domain.Entities;
using ECommerce.Domain.Repositories;
using ECommerce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ECommerce.Infrastructure.Repositories
{
    public class InventoryRepository : IInventoryRepository
    {
        private readonly ECommerceDbContext _db;

        public InventoryRepository(ECommerceDbContext db)
        {
            _db = db;
        }

        public async Task<InventoryItem> GetBySkuAsync(string sku)
        {
            return await _db.Inventory
                .FirstOrDefaultAsync(x => x.Sku == sku) 
                ?? throw new KeyNotFoundException($"Inventory {sku} not found");
        }

        public async Task<bool> TryReserveAsyncOld(string sku, int qty)
        {
            var affected = await _db.Database.ExecuteSqlRawAsync(
                @"UPDATE Inventory
            SET ReservedQty = ReservedQty + {0}
            WHERE Sku = {1}
            AND ActualQty - ReservedQty >= {0}",
                qty, sku);

            return affected > 0;
        }
        public async Task<bool> TryReserveAsync(string sku, int qty)
        {
            var inv = await _db.Inventory
                .SingleAsync(x => x.Sku == sku);

            inv.Reserve(qty);

            try
            {
                await _db.SaveChangesAsync();
                return true;
            }
            catch (DbUpdateConcurrencyException)
            {
                return false;
            }
        }

        public async Task ReleaseAsync(string sku, int qty)
        {
            var inv = await GetBySkuAsync(sku);
            inv.Release(qty);
        }

        public async Task CommitAsync(string sku, int qty)
        {
            var inv = await GetBySkuAsync(sku);
            inv.Commit(qty);
        }
    }
}
